using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlOrderExecutor {
    private const string LabelPrefix = "PDHPDL_V1";
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly Symbol _symbol;
    private readonly string _symbolName;

    private readonly double _riskPct;
    private readonly int _stopOffsetTicks;
    private readonly double _takeProfitR;
    private readonly PdhpdlEntryMode _entryMode;
    private readonly PdhpdlRiskGuard _riskGuard;

    private readonly string _timeFrame;
    private readonly PdhpdlTradeCsvLogger _csvLogger;
    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    public PdhpdlOrderExecutor(Robot robot, Symbol symbol, string symbolName, string timeFrame, double riskPct, int stopOffsetTicks,
        double takeProfitR, PdhpdlEntryMode entryMode, PdhpdlRiskGuard riskGuard, PdhpdlTradeCsvLogger csvLogger) {
        _robot = robot;
        _symbol = symbol;
        _symbolName = symbolName;
        _timeFrame = timeFrame;

        _riskPct = riskPct;
        _stopOffsetTicks = stopOffsetTicks;
        _takeProfitR = takeProfitR;
        _entryMode = entryMode;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;

        if (_riskGuard.NewsBlackoutWindowCount > 0)
            _robot.Print("*****News blackout windows loaded. Count: {0}", _riskGuard.NewsBlackoutWindowCount);

        _robot.Positions.Closed += OnPositionClosed;
        _robot.Positions.Opened += OnPositionOpened;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
        _robot.Positions.Opened -= OnPositionOpened;
    }

    public void ManageOpenPositions() {
        CloseExposureBeforeRiskWindow();
    }

    public void ExecuteIfSignal(PdhpdlSignal signal) {
        if (signal == null || !signal.HasData)
            return;

        if (!signal.IsLongSignal && !signal.IsShortSignal)
            return;

        if (IsNewOrderBlockedByRiskWindow()) {
            _robot.Print("*****Order skipped | Risk guard blocked new order. Time: {0}", _robot.Server.Time);
            return;
        }

        if (HasOpenSymbolPosition()) {
            _robot.Print("*****Order skipped | Existing position found on symbol: {0}", _symbolName);
            return;
        }

        if (HasOpenSymbolPendingOrder()) {
            _robot.Print("*****Order skipped | Existing pending order found on symbol: {0}", _symbolName);
            return;
        }

        PdhpdlOrderPlan plan = CreatePlan(signal);

        if (!plan.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", plan.RejectReason);
            return;
        }

        ExecutePlan(plan);
    }

    private bool HasOpenSymbolPosition() {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName);
    }

    private bool HasOpenSymbolPendingOrder() {
        return _robot.PendingOrders.Any(order => order.SymbolName == _symbolName);
    }

    private void CloseExposureBeforeRiskWindow() {
        if (!_riskGuard.ShouldForceClose(_robot.Server.Time))
            return;

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray()) {
            TradeResult result = _robot.CancelPendingOrder(order);

            if (!result.IsSuccessful)
                _robot.Print("*****Risk guard pending cancel failed | Order: {0}, Error: {1}", order.Id, result.Error);
        }

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray()) {
            TradeResult result = _robot.ClosePosition(position);

            if (!result.IsSuccessful)
                _robot.Print("*****Risk guard close failed | Position: {0}, Error: {1}", position.Id, result.Error);
        }
    }

    private bool IsNewOrderBlockedByRiskWindow() {
        return _riskGuard.ShouldBlockNewOrder(_robot.Server.Time);
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (!IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            entryEquity, _robot.Account.Equity);
        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);
    }

    private void OnPositionOpened(PositionOpenedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (_pendingCsvIdsByLabel.TryGetValue(args.Position.Label, out string csvId)) {
            _positionCsvIds[args.Position.Id] = csvId;
            _pendingCsvIdsByLabel.Remove(args.Position.Label);
        }

        if (_pendingEntryEquitiesByLabel.TryGetValue(args.Position.Label, out double entryEquity)) {
            _positionEntryEquities[args.Position.Id] = entryEquity;
            _pendingEntryEquitiesByLabel.Remove(args.Position.Label);
        }
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(LabelPrefix + "_");
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(LabelPrefix + "_");
    }

    private PdhpdlOrderPlan CreatePlan(PdhpdlSignal signal) {
        PdhpdlOrderPlan plan = new();

        TradeType tradeType = signal.IsLongSignal ? TradeType.Buy : TradeType.Sell;

        double closeEntry = signal.Close;
        double stopOffset = _symbol.TickSize * _stopOffsetTicks;

        double entry;
        double stop;
        double riskPrice;
        double takeProfit;

        if (tradeType == TradeType.Buy) {
            stop = signal.Low - stopOffset;
            entry = GetEntryPrice(closeEntry, stop, tradeType);
            riskPrice = entry - stop;
            takeProfit = entry + _takeProfitR * riskPrice;
        } else {
            stop = signal.High + stopOffset;
            entry = GetEntryPrice(closeEntry, stop, tradeType);
            riskPrice = stop - entry;
            takeProfit = entry - _takeProfitR * riskPrice;
        }

        if (_riskGuard.TryGetRiskPriceRejectReason(riskPrice, out string rejectReason)) {
            plan.RejectReason = rejectReason;
            return plan;
        }

        double stopLossPips = riskPrice / _symbol.PipSize;
        double takeProfitPips = Math.Abs(takeProfit - entry) / _symbol.PipSize;

        double accountEquity = _robot.Account.Equity;
        double riskMoney = _riskGuard.CalculateRiskMoney(accountEquity, _riskPct);

        double nativeRiskVolumeInUnits = _symbol.VolumeForProportionalRisk(
            ProportionalAmountType.Equity, _riskPct, stopLossPips, RoundingMode.Down);

        nativeRiskVolumeInUnits = _symbol.NormalizeVolumeInUnits(nativeRiskVolumeInUnits, RoundingMode.Down);

        double priceRiskCappedVolumeInUnits = riskMoney / riskPrice;
        priceRiskCappedVolumeInUnits = _symbol.NormalizeVolumeInUnits(priceRiskCappedVolumeInUnits, RoundingMode.Down);

        double totalVolumeInUnits = Math.Min(nativeRiskVolumeInUnits, priceRiskCappedVolumeInUnits);
        totalVolumeInUnits = _symbol.NormalizeVolumeInUnits(totalVolumeInUnits, RoundingMode.Down);

        if (totalVolumeInUnits < _symbol.VolumeInUnitsMin) {
            plan.RejectReason =
                $"Calculated volume is too small. TotalVolume={totalVolumeInUnits}, NativeVolume={nativeRiskVolumeInUnits}, PriceRiskCappedVolume={priceRiskCappedVolumeInUnits}, Min={_symbol.VolumeInUnitsMin}";
            return plan;
        }

        if (totalVolumeInUnits > _symbol.VolumeInUnitsMax) {
            plan.RejectReason =
                $"Calculated volume is above broker maximum. TotalVolume={totalVolumeInUnits}, Max={_symbol.VolumeInUnitsMax}";
            return plan;
        }

        double cappedRiskMoney = totalVolumeInUnits * riskPrice;

        if (cappedRiskMoney > riskMoney) {
            plan.RejectReason =
                $"Calculated volume exceeds risk limit. RiskMoney={riskMoney}, CappedRiskMoney={cappedRiskMoney}, TotalVolume={totalVolumeInUnits}";
            return plan;
        }

        double estimatedRiskMoney = _symbol.AmountRisked(totalVolumeInUnits, stopLossPips);

        string side = tradeType == TradeType.Buy ? "L" : "S";

        plan.IsValid = true;
        plan.TradeType = tradeType;
        plan.EntryMode = _entryMode;
        plan.IsMarketOrder = _entryMode == PdhpdlEntryMode.Close;
        plan.EntryPrice = entry;
        plan.StopPrice = stop;
        plan.TakeProfitPrice = takeProfit;
        plan.RiskPrice = riskPrice;
        plan.StopLossPips = stopLossPips;
        plan.TakeProfitPips = takeProfitPips;
        plan.TotalLots = totalVolumeInUnits / _symbol.LotSize;
        plan.TotalVolumeInUnits = totalVolumeInUnits;
        plan.NativeRiskVolumeInUnits = nativeRiskVolumeInUnits;
        plan.PriceRiskCappedVolumeInUnits = priceRiskCappedVolumeInUnits;
        plan.AccountEquity = accountEquity;
        plan.RiskMoney = riskMoney;
        plan.EstimatedRiskMoney = estimatedRiskMoney;
        plan.Label = $"{LabelPrefix}_{side}";

        return plan;
    }

    private double GetEntryPrice(double closeEntry, double stop, TradeType tradeType) {
        double ratio = GetPullbackRatio();

        if (ratio <= 0.0)
            return closeEntry;

        double distanceToStop = Math.Abs(closeEntry - stop);

        if (tradeType == TradeType.Buy)
            return closeEntry - distanceToStop * ratio;

        return closeEntry + distanceToStop * ratio;
    }

    private double GetPullbackRatio() {
        switch (_entryMode) {
            case PdhpdlEntryMode.Pullback25:
                return 0.25;
            case PdhpdlEntryMode.Pullback382:
                return 0.382;
            case PdhpdlEntryMode.Pullback50:
                return 0.50;
            default:
                return 0.0;
        }
    }

    private void ExecutePlan(PdhpdlOrderPlan plan) {
        _robot.Print(
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, NativeVolumeUnits: {9}, PriceRiskCappedVolumeUnits: {10}, Lots: {11}, TotalVolumeUnits: {12}",
            plan.TradeType, plan.EntryMode, plan.EntryPrice, plan.StopPrice, plan.TakeProfitPrice, plan.RiskPrice,
            plan.StopLossPips, plan.RiskMoney, plan.EstimatedRiskMoney, plan.NativeRiskVolumeInUnits, plan.PriceRiskCappedVolumeInUnits,
            plan.TotalLots, plan.TotalVolumeInUnits);

        TradeResult result = ExecuteOrder(plan);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return;
        }

        _robot.Print("*****Order submitted | Label: {0}", plan.Label);

        if (_entryMode == PdhpdlEntryMode.Close) {
            string csvId = _csvLogger.AppendEntry(plan, result.Position, _symbolName, _timeFrame);

            if (!string.IsNullOrWhiteSpace(csvId)) {
                _positionCsvIds[result.Position.Id] = csvId;
                _positionEntryEquities[result.Position.Id] = plan.AccountEquity;
                _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
            }
        } else {
            string csvId = _csvLogger.AppendPendingEntry(plan, result.PendingOrder, _symbolName, _timeFrame);

            if (!string.IsNullOrWhiteSpace(csvId)) {
                _pendingCsvIdsByLabel[result.PendingOrder.Label] = csvId;
                _pendingEntryEquitiesByLabel[result.PendingOrder.Label] = plan.AccountEquity;
                _robot.Print("*****CSV pending order record added. Id: {0}, Path: {1}", csvId, _csvLogger.FilePath);
            }
        }
    }

    private TradeResult ExecuteOrder(PdhpdlOrderPlan plan) {
        if (_entryMode == PdhpdlEntryMode.Close) {
            return _robot.ExecuteMarketOrder(plan.TradeType, _symbolName, plan.TotalVolumeInUnits, plan.Label, plan.StopLossPips,
                plan.TakeProfitPips, EntryComment);
        }

        return _robot.PlaceLimitOrder(plan.TradeType, _symbolName, plan.TotalVolumeInUnits, plan.EntryPrice, plan.Label, plan.StopLossPips,
            plan.TakeProfitPips, ProtectionType.Relative, null, EntryComment);
    }

    private string GetPositionCsvId(Position position) {
        if (_positionCsvIds.TryGetValue(position.Id, out string csvId))
            return csvId;

        return position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        if (_positionEntryEquities.TryGetValue(position.Id, out double entryEquity))
            return entryEquity;

        return 0.0;
    }
}
