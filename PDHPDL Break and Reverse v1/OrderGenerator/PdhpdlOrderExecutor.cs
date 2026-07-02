using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlOrderExecutor {
    private const string LabelPrefix = "PDHPDL_V1";
    private const string Tp1Comment = "TP1";
    private const string RunnerComment = "RUNNER";

    private readonly Robot _robot;
    private readonly Symbol _symbol;
    private readonly string _symbolName;

    private readonly double _riskPct;
    private readonly int _stopOffsetTicks;
    private readonly double _tp1R;
    private readonly double _tp2R;
    private readonly PdhpdlEntryMode _entryMode;

    private readonly string _timeFrame;
    private readonly PdhpdlTradeCsvLogger _csvLogger;
    private readonly Dictionary<string, PdhpdlOrderPlan> _pendingPlansByLabel = new();
    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();

    public PdhpdlOrderExecutor(Robot robot, Symbol symbol, string symbolName, string timeFrame, double riskPct, int stopOffsetTicks,
        double tp1R, double tp2R, PdhpdlEntryMode entryMode, PdhpdlTradeCsvLogger csvLogger) {
        _robot = robot;
        _symbol = symbol;
        _symbolName = symbolName;
        _timeFrame = timeFrame;

        _riskPct = riskPct;
        _stopOffsetTicks = stopOffsetTicks;
        _tp1R = tp1R;
        _tp2R = tp2R;
        _entryMode = entryMode;
        _csvLogger = csvLogger;

        _robot.Positions.Closed += OnPositionClosed;
        _robot.Positions.Opened += OnPositionOpened;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
        _robot.Positions.Opened -= OnPositionOpened;
    }

    public void ExecuteIfSignal(PdhpdlSignal signal) {
        if (signal == null || !signal.HasData)
            return;

        if (!signal.IsLongSignal && !signal.IsShortSignal)
            return;

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

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (!IsStrategyPosition(args.Position))
            return;

        WriteCloseCsvRecord(args.Position, args.Reason);
    }

    private void OnPositionOpened(PositionOpenedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (_pendingCsvIdsByLabel.TryGetValue(args.Position.Label, out string csvId)) {
            _positionCsvIds[args.Position.Id] = csvId;
            _pendingCsvIdsByLabel.Remove(args.Position.Label);
        }

        _pendingPlansByLabel.Remove(args.Position.Label);
    }

    private static string GetCloseReasonCode(PositionCloseReason reason) {
        switch (reason) {
            case PositionCloseReason.StopLoss:
                return "SL";
            case PositionCloseReason.StopOut:
                return "SO";
            case PositionCloseReason.TakeProfit:
                return "TP";
            default:
                return "CLOSE";
        }
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(LabelPrefix + "_");
    }

    private string GetCloseRecordId(string csvId, Position position, PositionCloseReason reason) {
        string legName = GetLegName(position.Label);

        if (reason == PositionCloseReason.TakeProfit)
            return $"{csvId}-{legName}";

        return $"{csvId}-{GetCloseReasonCode(reason)}-{legName}";
    }

    private static string GetLegName(string label) {
        if (!string.IsNullOrWhiteSpace(label) && label.EndsWith("_TP1"))
            return "TP1";

        return "TP2";
    }

    private static string GetOpenDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[0].Id.ToString();
    }

    private static string GetCloseDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[position.Deals.Count - 1].Id.ToString();
    }

    private PdhpdlOrderPlan CreatePlan(PdhpdlSignal signal) {
        PdhpdlOrderPlan plan = new();

        TradeType tradeType = signal.IsLongSignal ? TradeType.Buy : TradeType.Sell;

        double closeEntry = signal.Close;
        double stopOffset = _symbol.TickSize * _stopOffsetTicks;

        double entry;
        double stop;
        double riskPrice;
        double tp1;
        double tp2;

        if (tradeType == TradeType.Buy) {
            stop = signal.Low - stopOffset;
            entry = GetEntryPrice(closeEntry, stop, tradeType);
            riskPrice = entry - stop;
            tp1 = entry + _tp1R * riskPrice;
            tp2 = entry + _tp2R * riskPrice;
        } else {
            stop = signal.High + stopOffset;
            entry = GetEntryPrice(closeEntry, stop, tradeType);
            riskPrice = stop - entry;
            tp1 = entry - _tp1R * riskPrice;
            tp2 = entry - _tp2R * riskPrice;
        }

        if (riskPrice <= 0.0) {
            plan.RejectReason = "Risk price is not positive.";
            return plan;
        }

        double stopLossPips = riskPrice / _symbol.PipSize;
        double tp1Pips = Math.Abs(tp1 - entry) / _symbol.PipSize;
        double tp2Pips = Math.Abs(tp2 - entry) / _symbol.PipSize;

        double riskMoney = RiskUtil.CalcRiskMoney(_robot.Account.Equity, _riskPct);

        double totalVolumeInUnits = _symbol.VolumeForProportionalRisk(
            ProportionalAmountType.Equity, _riskPct, stopLossPips, RoundingMode.Down);

        totalVolumeInUnits = _symbol.NormalizeVolumeInUnits(totalVolumeInUnits, RoundingMode.Down);

        if (totalVolumeInUnits < _symbol.VolumeInUnitsMin) {
            plan.RejectReason = $"Calculated volume is too small. TotalVolume={totalVolumeInUnits}, Min={_symbol.VolumeInUnitsMin}";
            return plan;
        }

        if (totalVolumeInUnits > _symbol.VolumeInUnitsMax) {
            plan.RejectReason =
                $"Calculated volume is above broker maximum. TotalVolume={totalVolumeInUnits}, Max={_symbol.VolumeInUnitsMax}";
            return plan;
        }

        double volumePerLegInUnits = _symbol.NormalizeVolumeInUnits(totalVolumeInUnits / 2.0, RoundingMode.Down);

        if (volumePerLegInUnits < _symbol.VolumeInUnitsMin) {
            plan.RejectReason = $"Volume per leg is too small. VolumePerLeg={volumePerLegInUnits}, Min={_symbol.VolumeInUnitsMin}";
            return plan;
        }

        double executableTotalVolumeInUnits = volumePerLegInUnits * 2.0;
        double estimatedRiskMoney = _symbol.AmountRisked(executableTotalVolumeInUnits, stopLossPips);

        string side = tradeType == TradeType.Buy ? "L" : "S";

        plan.IsValid = true;
        plan.TradeType = tradeType;
        plan.EntryMode = _entryMode;
        plan.IsMarketOrder = _entryMode == PdhpdlEntryMode.Close;
        plan.EntryPrice = entry;
        plan.StopPrice = stop;
        plan.Tp1Price = tp1;
        plan.Tp2Price = tp2;
        plan.RiskPrice = riskPrice;
        plan.StopLossPips = stopLossPips;
        plan.Tp1Pips = tp1Pips;
        plan.Tp2Pips = tp2Pips;
        plan.TotalLots = executableTotalVolumeInUnits / _symbol.LotSize;
        plan.TotalVolumeInUnits = executableTotalVolumeInUnits;
        plan.VolumePerLegInUnits = volumePerLegInUnits;
        plan.RiskMoney = riskMoney;
        plan.EstimatedRiskMoney = estimatedRiskMoney;
        plan.Tp1Label = $"{LabelPrefix}_{side}_TP1";
        plan.RunnerLabel = $"{LabelPrefix}_{side}_RUNNER";

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
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TP1: {4}, TP2: {5}, RiskPrice: {6}, StopLossPips: {7}, RiskMoney: {8}, EstimatedRiskMoney: {9}, Lots: {10}, TotalVolumeUnits: {11}, VolumePerLegUnits: {12}",
            plan.TradeType, plan.EntryMode, plan.EntryPrice, plan.StopPrice, plan.Tp1Price, plan.Tp2Price, plan.RiskPrice,
            plan.StopLossPips, plan.RiskMoney, plan.EstimatedRiskMoney, plan.TotalLots, plan.TotalVolumeInUnits, plan.VolumePerLegInUnits);

        TradeResult tp1Result = ExecuteLeg(plan.TradeType, plan.Tp1Label, plan.VolumePerLegInUnits, plan.EntryPrice, plan.StopLossPips,
            plan.Tp1Pips, Tp1Comment);

        if (!tp1Result.IsSuccessful) {
            _robot.Print("*****TP1 order failed | Error: {0}", tp1Result.Error);
            return;
        }

        TradeResult runnerResult = ExecuteLeg(plan.TradeType, plan.RunnerLabel, plan.VolumePerLegInUnits, plan.EntryPrice,
            plan.StopLossPips, plan.Tp2Pips, RunnerComment);

        if (!runnerResult.IsSuccessful) {
            _robot.Print("*****Runner order failed | Error: {0}", runnerResult.Error);

            if (tp1Result.Position != null)
                _robot.ClosePosition(tp1Result.Position);

            if (tp1Result.PendingOrder != null)
                _robot.CancelPendingOrder(tp1Result.PendingOrder);

            return;
        }

        _robot.Print("*****Orders submitted | TP1: {0}, Runner: {1}", plan.Tp1Label, plan.RunnerLabel);

        if (_entryMode == PdhpdlEntryMode.Close) {
            WriteEntryCsvRecord(plan, tp1Result.Position, "TP1");
            WriteEntryCsvRecord(plan, runnerResult.Position, "TP2");
        } else {
            _pendingPlansByLabel[plan.Tp1Label] = plan;
            _pendingPlansByLabel[plan.RunnerLabel] = plan;
            WritePendingEntryCsvRecord(plan, tp1Result.PendingOrder, "TP1");
            WritePendingEntryCsvRecord(plan, runnerResult.PendingOrder, "TP2");
        }
    }

    private TradeResult ExecuteLeg(TradeType tradeType, string label, double volumeInUnits, double entryPrice, double stopLossPips,
        double takeProfitPips, string comment) {
        if (_entryMode == PdhpdlEntryMode.Close) {
            return _robot.ExecuteMarketOrder(tradeType, _symbolName, volumeInUnits, label, stopLossPips, takeProfitPips, comment);
        }

        return _robot.PlaceLimitOrder(tradeType, _symbolName, volumeInUnits, entryPrice, label, stopLossPips, takeProfitPips,
            ProtectionType.Relative, null, comment);
    }

    private void WriteEntryCsvRecord(PdhpdlOrderPlan plan, Position position, string legName) {
        if (position == null)
            return;

        try {
            string side = plan.TradeType == TradeType.Buy ? "B" : "S";
            string keyLevel = plan.TradeType == TradeType.Buy ? "PDL" : "PDH";

            var record = new PdhpdlTradeCsvRecord {
                Id = position.Id.ToString(),
                Side = side,
                KeyLevel = keyLevel,
                Signal = "false-breakout",
                CloseEntryResult = GetEntryModeCsvValue(PdhpdlEntryMode.Close, plan.EntryMode),
                Pullback25Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback25, plan.EntryMode),
                Pullback382Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback382, plan.EntryMode),
                Pullback50Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback50, plan.EntryMode),
                Comment = legName,
                Symbol = _symbolName,
                TimeFrame = _timeFrame,
                EntryTime = position.EntryTime,
                EntryPrice = position.EntryPrice,
                StopPrice = plan.StopPrice,
                Tp1Price = plan.Tp1Price,
                Tp2Price = plan.Tp2Price,
                RiskPrice = plan.RiskPrice,
                VolumeInUnits = position.VolumeInUnits,
                PositionId = position.Id.ToString(),
                DealId = GetOpenDealId(position)
            };

            _positionCsvIds[position.Id] = record.Id;
            _csvLogger.Append(record);

            _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        } catch (Exception ex) {
            _robot.Print("*****CSV write failed | {0}", ex.Message);
        }
    }

    private void WritePendingEntryCsvRecord(PdhpdlOrderPlan plan, PendingOrder order, string legName) {
        if (order == null)
            return;

        try {
            string side = plan.TradeType == TradeType.Buy ? "B" : "S";
            string keyLevel = plan.TradeType == TradeType.Buy ? "PDL" : "PDH";
            string csvId = order.Id.ToString();

            var record = new PdhpdlTradeCsvRecord {
                Id = csvId,
                Side = side,
                KeyLevel = keyLevel,
                Signal = "false-breakout",
                CloseEntryResult = GetEntryModeCsvValue(PdhpdlEntryMode.Close, plan.EntryMode),
                Pullback25Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback25, plan.EntryMode),
                Pullback382Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback382, plan.EntryMode),
                Pullback50Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback50, plan.EntryMode),
                Comment = legName,
                Symbol = _symbolName,
                TimeFrame = _timeFrame,
                EntryTime = order.SubmittedTime,
                EntryPrice = order.TargetPrice,
                StopPrice = plan.StopPrice,
                Tp1Price = plan.Tp1Price,
                Tp2Price = plan.Tp2Price,
                RiskPrice = plan.RiskPrice,
                VolumeInUnits = order.VolumeInUnits,
                PendingOrderId = csvId
            };

            _pendingCsvIdsByLabel[order.Label] = csvId;
            _csvLogger.Append(record);

            _robot.Print("*****CSV pending order record added. Id: {0}, Path: {1}", record.Id, _csvLogger.FilePath);
        } catch (Exception ex) {
            _robot.Print("*****CSV pending order write failed | {0}", ex.Message);
        }
    }

    private void WriteCloseCsvRecord(Position position, PositionCloseReason reason) {
        try {
            string closeReason = GetCloseReasonCode(reason);
            string csvId = GetPositionCsvId(position);

            var record = new PdhpdlTradeCsvRecord {
                Id = GetCloseRecordId(csvId, position, reason),
                Side = position.TradeType == TradeType.Buy ? "B" : "S",
                KeyLevel = "",
                Signal = "close",
                CloseEntryResult = "",
                Pullback25Result = "",
                Pullback382Result = "",
                Pullback50Result = "",
                Comment = position.NetProfit >= 0.0 ? "盈利" : "亏损",
                Symbol = _symbolName,
                TimeFrame = _timeFrame,
                EntryTime = position.EntryTime,
                EntryPrice = position.EntryPrice,
                StopPrice = 0.0,
                Tp1Price = 0.0,
                Tp2Price = 0.0,
                RiskPrice = 0.0,
                VolumeInUnits = position.VolumeInUnits,
                CloseReason = closeReason,
                ProfitLoss = position.NetProfit,
                CloseTime = _robot.Server.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                PositionId = position.Id.ToString(),
                DealId = GetCloseDealId(position)
            };

            _csvLogger.Append(record);
            _positionCsvIds.Remove(position.Id);

            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", record.Id, record.ProfitLoss);
        } catch (Exception ex) {
            _robot.Print("*****CSV close write failed | {0}", ex.Message);
        }
    }

    private string GetPositionCsvId(Position position) {
        if (_positionCsvIds.TryGetValue(position.Id, out string csvId))
            return csvId;

        return position.Id.ToString();
    }

    private static string GetEntryModeCsvValue(PdhpdlEntryMode columnMode, PdhpdlEntryMode selectedMode) {
        return columnMode == selectedMode ? "ORDER" : "";
    }
}
