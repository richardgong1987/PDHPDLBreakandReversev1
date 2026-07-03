using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and tracks the strategy's cTrader orders. It gates on the risk guard and open
// exposure, asks PdhpdlOrderPlanner to size the order, submits it, and keeps the CSV
// row ids so opens and closes can be reconciled. All sizing math lives in the planner.
public class PdhpdlOrderExecutor {
    private const string StrategyLabelPrefix = PdhpdlOrderPlanner.LabelPrefix + "_";
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly PdhpdlTradeCsvLogger _csvLogger;

    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, PdhpdlOrderPlanner planner,
        PdhpdlRiskGuard riskGuard, PdhpdlTradeCsvLogger csvLogger) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _planner = planner;
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

        if (_riskGuard.ShouldBlockNewOrder(_robot.Server.Time)) {
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

        PdhpdlOrderPlan plan = _planner.CreatePlan(signal, _robot.Account.Equity);

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

    private void ExecutePlan(PdhpdlOrderPlan plan) {
        _robot.Print(
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, NativeVolumeUnits: {9}, PriceRiskCappedVolumeUnits: {10}, Lots: {11}, TotalVolumeUnits: {12}",
            plan.Direction, plan.EntryMode, plan.EntryPrice, plan.StopPrice, plan.TakeProfitPrice, plan.RiskPrice,
            plan.StopLossPips, plan.RiskMoney, plan.EstimatedRiskMoney, plan.NativeRiskVolumeInUnits, plan.PriceRiskCappedVolumeInUnits,
            plan.TotalLots, plan.TotalVolumeInUnits);

        TradeResult result = SubmitOrder(plan);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return;
        }

        _robot.Print("*****Order submitted | Label: {0}", plan.Label);

        if (plan.IsMarketOrder)
            RecordMarketEntry(plan, result.Position);
        else
            RecordPendingEntry(plan, result.PendingOrder);
    }

    private TradeResult SubmitOrder(PdhpdlOrderPlan plan) {
        TradeType tradeType = ToTradeType(plan.Direction);

        if (plan.IsMarketOrder) {
            return _robot.ExecuteMarketOrder(tradeType, _symbolName, plan.TotalVolumeInUnits, plan.Label, plan.StopLossPips,
                plan.TakeProfitPips, EntryComment);
        }

        return _robot.PlaceLimitOrder(tradeType, _symbolName, plan.TotalVolumeInUnits, plan.EntryPrice, plan.Label, plan.StopLossPips,
            plan.TakeProfitPips, ProtectionType.Relative, null, EntryComment);
    }

    private void RecordMarketEntry(PdhpdlOrderPlan plan, Position position) {
        string csvId = _csvLogger.AppendEntry(plan, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = plan.AccountEquity;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
    }

    private void RecordPendingEntry(PdhpdlOrderPlan plan, PendingOrder order) {
        string csvId = _csvLogger.AppendPendingEntry(plan, order, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return;

        _pendingCsvIdsByLabel[order.Label] = csvId;
        _pendingEntryEquitiesByLabel[order.Label] = plan.AccountEquity;
        _robot.Print("*****CSV pending order record added. Id: {0}, Path: {1}", csvId, _csvLogger.FilePath);
    }

    private void OnPositionOpened(PositionOpenedEventArgs args) {
        if (args?.Position == null)
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

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
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

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(StrategyLabelPrefix);
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) &&
               order.Label.StartsWith(StrategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
    }

    private static TradeType ToTradeType(PdhpdlTradeDirection direction) {
        return direction == PdhpdlTradeDirection.Long ? TradeType.Buy : TradeType.Sell;
    }
}
