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

    public void ExecuteIfSignal(PdhpdlSignalModel signalModel) {
        if (signalModel == null || !signalModel.HasData)
            return;

        if (!signalModel.IsLongSignal && !signalModel.IsShortSignal)
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

        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return;
        }

        ExecutePlan(planModel);
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

    private void ExecutePlan(PdhpdlOrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.DirectionModel, planModel.EntryModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice, planModel.RiskPrice,
            planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney,
            planModel.Lots, planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return;
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);

        if (planModel.IsMarketOrder)
            RecordMarketEntry(planModel, result.Position);
        else
            RecordPendingEntry(planModel, result.PendingOrder);
    }

    private TradeResult SubmitOrder(PdhpdlOrderPlanModel planModel) {
        TradeType tradeType = ToTradeType(planModel.DirectionModel);

        if (planModel.IsMarketOrder) {
            return _robot.ExecuteMarketOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.Label, planModel.StopLossPips,
                planModel.TakeProfitPips, EntryComment);
        }

        return _robot.PlaceLimitOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.EntryPrice, planModel.Label, planModel.StopLossPips,
            planModel.TakeProfitPips, ProtectionType.Relative, null, EntryComment);
    }

    private void RecordMarketEntry(PdhpdlOrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
    }

    private void RecordPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order) {
        string csvId = _csvLogger.AppendPendingEntry(planModel, order, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return;

        _pendingCsvIdsByLabel[order.Label] = csvId;
        _pendingEntryEquitiesByLabel[order.Label] = planModel.AccountEquity;
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

    private static TradeType ToTradeType(PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
