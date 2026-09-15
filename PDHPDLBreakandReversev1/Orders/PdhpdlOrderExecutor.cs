using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and tracks the strategy's cTrader orders. It gates on the risk guard and open
// exposure, asks PdhpdlOrderPlanner to size the order, submits it, and keeps the CSV
// row ids so opens and closes can be reconciled. All sizing math lives in the planner.
public class PdhpdlOrderExecutor {
    private const string EntryComment = "ENTRY";

    // 挂单最多等 3 根收盘 K 线；等不到回撤就撤单。
    private const int PendingOrderExpiryBars = 3;

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    // Orders are labelled "{OrderLabel}_L" / "{OrderLabel}_S". The side suffix keeps simultaneous long and
    // short pending orders (MultiplePosition) apart in the per-label CSV maps; the prefix marks this
    // instance's orders, so manual trades and other bots on the same symbol are ignored.
    private readonly string _strategyLabelPrefix;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly PdhpdlTradeCsvLogger _csvLogger;

    // 连亏锁仓。上锁/解锁的状态机在它自己里面，这里只负责喂平仓结果、以及开单前问一句锁没锁。
    private readonly PivotEntryGate _entryGate;
    private readonly ConsecutiveLossCounter _lossCounter;

    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<int, int> _pendingOrderBarIndexById = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();
    private readonly Dictionary<string, EntryGateSnapshot> _pendingGateSnapshotsByLabel = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, string orderLabel, PdhpdlOrderPlanner planner,
        PdhpdlRiskGuard riskGuard, PdhpdlTradeCsvLogger csvLogger, PivotEntryGate entryGate, ConsecutiveLossCounter lossCounter) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _strategyLabelPrefix = orderLabel + "_";
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;
        _entryGate = entryGate;
        _lossCounter = lossCounter;

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

        if (signalModel.Strategy != StrategyModel.MultiplePosition && HasStrategyOrderOrPosition()) {
            _robot.Print("*****Order skipped | Label {0}* already has a pending order or open position on symbol: {1}",
                _strategyLabelPrefix, _symbolName);
            return false;
        }

        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return false;
        }

        planModel.Label = _strategyLabelPrefix + (planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "L" : "S");
        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.KeyLevel;
        planModel.SignalBarIndex = signalModel.BarIndex;
        planModel.AtrRatioH1 = signalModel.AtrRatioH1;
        planModel.PdRangeAtr = signalModel.PdRangeAtr;
        planModel.Adx14H1 = signalModel.Adx14H1;
        planModel.Adx14H1Previous = signalModel.Adx14H1Previous;
        planModel.DiPlus14H1 = signalModel.DiPlus14H1;
        planModel.DiMinus14H1 = signalModel.DiMinus14H1;
        planModel.GapExpansionX3Bar = signalModel.GapExpansionX3Bar;
        planModel.GapExpansionX1Bar = signalModel.GapExpansionX1Bar;

        // 快照必须在下单之前放好：市价单的 Positions.Opened 可能在 SubmitOrder 里就回调了。
        _pendingGateSnapshotsByLabel[planModel.Label] = new EntryGateSnapshot(planModel.DirectionModel, signalModel.PivotCount);

        if (ExecutePlan(planModel))
            return true;

        _pendingGateSnapshotsByLabel.Remove(planModel.Label);
        return false;
    }

    // Checks live broker state rather than in-memory maps, so a restart does not stack a second order.
    private bool HasStrategyOrderOrPosition() {
        return _robot.PendingOrders.Any(IsStrategyPendingOrder) || _robot.Positions.Any(IsStrategyPosition);
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
        _pendingGateSnapshotsByLabel.Remove(order.Label);
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

    private bool ExecutePlan(PdhpdlOrderPlanModel planModel) {
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
            return RecordMarketEntry(planModel, result.Position);
        }

        return RecordPendingEntry(planModel, result.PendingOrder);
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

    private bool RecordMarketEntry(PdhpdlOrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private bool RecordPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order) {
        string csvId = _csvLogger.AppendPendingEntry(planModel, order, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _pendingCsvIdsByLabel[order.Label] = csvId;
        _pendingEntryEquitiesByLabel[order.Label] = planModel.AccountEquity;
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

        RecordEntryForGate(args.Position.Label);
    }

    // 仓位真正开出来才算吃掉一个令牌。用的是下单那一刻的结构点编号，也就是闸门放行时比对过的
    // 那个基准，这样「一个结构点放行一笔」才对得上：挂单成交时可能又新出了几个结构点，
    // 拿成交那一刻的编号记账会把它们一并当成已经用掉。
    private void RecordEntryForGate(string label) {
        if (string.IsNullOrWhiteSpace(label) || !_pendingGateSnapshotsByLabel.TryGetValue(label, out EntryGateSnapshot snapshot))
            return;

        _pendingGateSnapshotsByLabel.Remove(label);
        _entryGate.RecordEntry(snapshot.Direction, snapshot.PivotCount);
        _robot.Print("*****Entry recorded | Side: {0}, PivotCount: {1}", snapshot.Direction, snapshot.PivotCount);
    }

    private readonly struct EntryGateSnapshot {
        public EntryGateSnapshot(PdhpdlTradeDirectionModel direction, int pivotCount) {
            Direction = direction;
            PivotCount = pivotCount;
        }

        public PdhpdlTradeDirectionModel Direction { get; }
        public int PivotCount { get; }
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);

        _lossCounter.RecordClosedTrade(args.Position.NetProfit);
        _robot.Print("*****Consecutive losses | Count: {0}, PivotGateRequired: {1}", _lossCounter.ConsecutiveLosses,
            _lossCounter.IsPivotGateRequired);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(_strategyLabelPrefix);
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(_strategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
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
