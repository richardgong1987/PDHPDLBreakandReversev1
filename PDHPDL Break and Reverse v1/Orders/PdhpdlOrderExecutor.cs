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

    // 挂单最多等 3 根收盘 K 线；等不到回撤就撤单。
    private const int PendingOrderExpiryBars = 3;

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly PdhpdlTradeCsvLogger _csvLogger;

    // 连亏锁仓。上锁/解锁的状态机在它自己里面，这里只负责喂平仓结果、以及开单前问一句锁没锁。
    private readonly ConsecutiveLossLock _lossLock;

    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryAtrByLabel = new();
    private readonly Dictionary<int, int> _pendingOrderBarIndexById = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();
    private readonly Dictionary<int, double> _positionEntryAtr = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, PdhpdlOrderPlanner planner, PdhpdlRiskGuard riskGuard,
        PdhpdlTradeCsvLogger csvLogger, ConsecutiveLossLock lossLock) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;
        _lossLock = lossLock;

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

    public bool ExecuteIfSignal(PdhpdlSignalModel signalModel) {
        if (signalModel == null || !signalModel.HasData)
            return false;

        if (!signalModel.IsLongSignal && !signalModel.IsShortSignal)
            return false;

        if (_riskGuard.ShouldBlockNewOrder(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Risk guard blocked new order. Time: {0}", _robot.Server.Time);
            return false;
        }

        if (signalModel.Strategy != StrategyModel.MultiplePosition && (HasOpenSymbolPosition() || HasOpenSymbolPendingOrder())) {
            _robot.Print("*****Order skipped | Existing position found on symbol: {0}", _symbolName);
            return false;
        }


        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return false;
        }

        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.KeyLevel;
        planModel.SignalBarIndex = signalModel.BarIndex;

        if (IsLockedByConsecutiveLosses())
            return false;

        return ExecutePlan(planModel, signalModel.Atr);
    }

    // 锁仓期间不开新单。解锁由 ConsecutiveLossLock 在每根收盘 K 线上判断，与信号无关。
    private bool IsLockedByConsecutiveLosses() {
        if (!_lossLock.IsLocked)
            return false;

        _robot.Print("*****Order skipped | Locked after {0} consecutive losses. Price must move {1} from {2} (frozen ATR {3})",
            _lossLock.ConsecutiveLosses, _lossLock.RequiredDistance, _lossLock.AnchorPrice, _lossLock.LockedAtr);
        return true;
    }

    private bool HasOpenSymbolPosition() {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName);
    }

    private bool HasOpenSymbolPendingOrder() {
        return _robot.PendingOrders.Any(order => order.SymbolName == _symbolName);
    }

    // 大 K 线的挂单是「等价格回撤到中点」，回撤没来就说明这笔已经作废：只给它 PendingOrderExpiryBars
    // 根收盘 K 线的时间，超时撤单，避免行情早已走远后挂单还在原地等着被扫。
    public void CancelExpiredPendingOrders(int closedBarIndex) {
        ForgetFilledPendingOrders();

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray()) {
            if (!IsPendingOrderExpired(order, closedBarIndex))
                continue;

            CancelPendingOrder(order, $"unfilled after {PendingOrderExpiryBars} bars");
        }
    }

    private bool IsPendingOrderExpired(PendingOrder order, int closedBarIndex) {
        // 本次运行之前就存在的挂单没有下单 K 线记录，不归这条规则管。
        if (!_pendingOrderBarIndexById.TryGetValue(order.Id, out int placedBarIndex))
            return false;

        return closedBarIndex - placedBarIndex >= PendingOrderExpiryBars;
    }

    private void ForgetFilledPendingOrders() {
        HashSet<int> liveOrderIds = new(_robot.PendingOrders.Select(order => order.Id));

        foreach (int orderId in _pendingOrderBarIndexById.Keys.Where(id => !liveOrderIds.Contains(id)).ToArray())
            _pendingOrderBarIndexById.Remove(orderId);
    }

    private void CancelPendingOrder(PendingOrder order, string reason) {
        TradeResult result = _robot.CancelPendingOrder(order);

        if (!result.IsSuccessful) {
            _robot.Print("*****Pending cancel failed | Order: {0}, Reason: {1}, Error: {2}", order.Id, reason, result.Error);
            return;
        }

        ForgetCancelledPendingOrder(order);
        _robot.Print("*****Pending order cancelled | Order: {0}, Reason: {1}", order.Id, reason);
    }

    // 撤单后必须把这笔挂单的 CSV 行号/权益/ATR 一起丢掉，否则同 label 的下一笔持仓会认领到它的旧记录。
    private void ForgetCancelledPendingOrder(PendingOrder order) {
        _pendingOrderBarIndexById.Remove(order.Id);

        if (_robot.PendingOrders.Any(other => other.Id != order.Id && other.Label == order.Label))
            return;

        _pendingCsvIdsByLabel.Remove(order.Label);
        _pendingEntryEquitiesByLabel.Remove(order.Label);
        _pendingEntryAtrByLabel.Remove(order.Label);
    }

    private void CloseExposureBeforeRiskWindow() {
        if (!_riskGuard.ShouldForceClose(_robot.Server.Time))
            return;

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray())
            CancelPendingOrder(order, "risk guard force close");

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray()) {
            TradeResult result = _robot.ClosePosition(position);

            if (!result.IsSuccessful)
                _robot.Print("*****Risk guard close failed | Position: {0}, Error: {1}", position.Id, result.Error);
        }
    }

    private bool ExecutePlan(PdhpdlOrderPlanModel planModel, double entryAtr) {
        _robot.Print(
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.DirectionModel, planModel.EntryModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots,
            planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return false;
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);

        if (planModel.IsMarketOrder) {
            return RecordMarketEntry(planModel, result.Position, entryAtr);
        }

        return RecordPendingEntry(planModel, result.PendingOrder, entryAtr);
    }

    private TradeResult SubmitOrder(PdhpdlOrderPlanModel planModel) {
        TradeType tradeType = ToTradeType(planModel.DirectionModel);

        if (planModel.IsMarketOrder) {
            return _robot.ExecuteMarketOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.Label, planModel.StopLossPips,
                planModel.TakeProfitPips, EntryComment);
        }

        return _robot.PlaceLimitOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.EntryPrice, planModel.Label,
            planModel.StopLossPips, planModel.TakeProfitPips, ProtectionType.Relative, null, EntryComment);
    }

    private bool RecordMarketEntry(PdhpdlOrderPlanModel planModel, Position position, double entryAtr) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _positionEntryAtr[position.Id] = entryAtr;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private bool RecordPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order, double entryAtr) {
        string csvId = _csvLogger.AppendPendingEntry(planModel, order, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _pendingCsvIdsByLabel[order.Label] = csvId;
        _pendingEntryEquitiesByLabel[order.Label] = planModel.AccountEquity;
        _pendingEntryAtrByLabel[order.Label] = entryAtr;
        _pendingOrderBarIndexById[order.Id] = planModel.SignalBarIndex;
        _robot.Print("*****CSV pending order record added. Id: {0}, Path: {1}", csvId, _csvLogger.FilePath);
        return true;
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

        if (_pendingEntryAtrByLabel.TryGetValue(args.Position.Label, out double entryAtr)) {
            _positionEntryAtr[args.Position.Id] = entryAtr;
            _pendingEntryAtrByLabel.Remove(args.Position.Label);
        }
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity);

        double entryAtr = GetPositionEntryAtr(args.Position);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);
        _positionEntryAtr.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);

        _lossLock.RecordClosedTrade(args.Position.NetProfit, entryAtr, args.Position.EntryPrice);
        _robot.Print("*****Consecutive losses | Count: {0}, Locked: {1}, Anchor: {2}, NeedsMoveOf: {3}", _lossLock.ConsecutiveLosses,
            _lossLock.IsLocked, _lossLock.AnchorPrice, _lossLock.RequiredDistance);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(StrategyLabelPrefix);
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(StrategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
    }

    private double GetPositionEntryAtr(Position position) {
        return _positionEntryAtr.TryGetValue(position.Id, out double entryAtr) ? entryAtr : 0.0;
    }

    private double GetClosePrice(Position position) {
        HistoricalTrade[] closedTrades = _robot.History.FindByPositionId(position.Id);

        if (closedTrades != null && closedTrades.Length > 0)
            return closedTrades.OrderByDescending(trade => trade.ClosingTime).First().ClosingPrice;

        for (int i = position.Deals.Count - 1; i >= 0; i--) {
            Deal deal = position.Deals[i];

            if (deal.PositionImpact == DealPositionImpact.Closing && deal.ExecutionPrice.HasValue)
                return deal.ExecutionPrice.Value;
        }

        return 0.0;
    }

    private static TradeType ToTradeType(PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
