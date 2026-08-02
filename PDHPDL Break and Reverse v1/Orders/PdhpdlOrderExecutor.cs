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

    // 连亏笔数 + 最近那笔亏损单开仓时的 ATR，驱动下面的连亏锁仓。
    private readonly ConsecutiveLossCounter _lossCounter = new();

    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryAtrByLabel = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();
    private readonly Dictionary<int, double> _positionEntryAtr = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, PdhpdlOrderPlanner planner, PdhpdlRiskGuard riskGuard,
        PdhpdlTradeCsvLogger csvLogger) {
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

    public int LossCount() {
        return _lossCounter.Count;
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

        if (IsLockedByConsecutiveLosses(signalModel))
            return false;

        return ExecutePlan(planModel, signalModel.Atr);
    }

    // 连亏达到 Nlock 笔后锁仓，直到某根信号 K 线的振幅超过「最近那笔亏损单开仓时 ATR」的
    // MaxBarRangeAtr 倍——行情比我上次入场时活跃这么多，才算走出来，放行这一单。
    // 参照用的是当时冻结的 ATR 而不是当前 ATR：行情冷下来时当前 ATR 会一起降、门槛跟着降，
    // 等于自动解锁，锁仓就白锁了。
    private bool IsLockedByConsecutiveLosses(PdhpdlSignalModel signalModel) {
        if (signalModel.Nlock <= 0 || signalModel.LossCount < signalModel.Nlock)
            return false;

        if (HasLeftLossRegime(signalModel))
            return false;

        _robot.Print("*****Order skipped | Locked after {0} consecutive losses. BarRange: {1}, Needed: > {2} ({3} x ATR {4} at last losing entry)",
            _lossCounter.Count, signalModel.High - signalModel.Low, RequiredBarRange(signalModel), signalModel.MaxBarRangeAtr,
            _lossCounter.AtrAtLastLossEntry);
        return true;
    }

    private bool HasLeftLossRegime(PdhpdlSignalModel signalModel) {
        // 没记到参照 ATR（例如重启后连亏计数还在但开仓 ATR 已丢）时不拦，宁可放行也不无限期锁死。
        if (_lossCounter.AtrAtLastLossEntry <= 0.0)
            return true;

        return signalModel.High - signalModel.Low > RequiredBarRange(signalModel);
    }

    private double RequiredBarRange(PdhpdlSignalModel signalModel) {
        return _lossCounter.AtrAtLastLossEntry * signalModel.MaxBarRangeAtr;
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

        _lossCounter.RecordClosedTrade(args.Position.NetProfit, entryAtr);
        _robot.Print("*****Consecutive losses | Count: {0}, AtrAtLastLossEntry: {1}", _lossCounter.Count,
            _lossCounter.AtrAtLastLossEntry);
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
