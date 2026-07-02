using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly double _tp1R;
    private readonly double _tp2R;
    private readonly PdhpdlEntryMode _entryMode;
    private readonly bool _enableSessionRiskGuard;
    private readonly int _noNewOrdersStartHour;
    private readonly int _forceCloseHour;
    private readonly int _forceCloseMinute;
    private readonly int _resumeTradingHour;
    private readonly List<NewsBlackoutWindow> _newsBlackoutWindows;

    private readonly string _timeFrame;
    private readonly PdhpdlTradeCsvLogger _csvLogger;
    private readonly Dictionary<string, PdhpdlOrderPlan> _pendingPlansByLabel = new();
    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, Tp1State> _tp1States = new();

    public PdhpdlOrderExecutor(Robot robot, Symbol symbol, string symbolName, string timeFrame, double riskPct, int stopOffsetTicks,
        double tp1R, double tp2R, PdhpdlEntryMode entryMode, bool enableSessionRiskGuard, int noNewOrdersStartHour, int forceCloseHour,
        int forceCloseMinute, int resumeTradingHour, string newsBlackoutWindows, PdhpdlTradeCsvLogger csvLogger) {
        _robot = robot;
        _symbol = symbol;
        _symbolName = symbolName;
        _timeFrame = timeFrame;

        _riskPct = riskPct;
        _stopOffsetTicks = stopOffsetTicks;
        _tp1R = tp1R;
        _tp2R = tp2R;
        _entryMode = entryMode;
        _enableSessionRiskGuard = enableSessionRiskGuard;
        _noNewOrdersStartHour = noNewOrdersStartHour;
        _forceCloseHour = forceCloseHour;
        _forceCloseMinute = forceCloseMinute;
        _resumeTradingHour = resumeTradingHour;
        _newsBlackoutWindows = ParseNewsBlackoutWindows(newsBlackoutWindows);
        _csvLogger = csvLogger;

        if (_newsBlackoutWindows.Count > 0)
            _robot.Print("*****News blackout windows loaded. Count: {0}", _newsBlackoutWindows.Count);

        _robot.Positions.Closed += OnPositionClosed;
        _robot.Positions.Opened += OnPositionOpened;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
        _robot.Positions.Opened -= OnPositionOpened;
    }

    public void ManageOpenPositions() {
        CloseExposureBeforeRiskWindow();

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray())
            TryCloseTp1(position);
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
        if (!IsForceCloseTime(_robot.Server.Time) && !IsInNewsBlackout(_robot.Server.Time))
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
        DateTime time = _robot.Server.Time;

        if (IsInNewsBlackout(time))
            return true;

        if (!_enableSessionRiskGuard)
            return false;

        if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
            return true;

        return IsInNoNewOrderWindow(time);
    }

    private bool IsForceCloseTime(DateTime time) {
        if (!_enableSessionRiskGuard)
            return false;

        if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
            return true;

        return IsInForceCloseWindow(time);
    }

    private bool IsInNoNewOrderWindow(DateTime time) {
        int currentMinutes = GetMinutesOfDay(time);
        int startMinutes = _noNewOrdersStartHour * 60;
        int resumeMinutes = _resumeTradingHour * 60;

        return IsWithinWindow(currentMinutes, startMinutes, resumeMinutes);
    }

    private bool IsInForceCloseWindow(DateTime time) {
        int currentMinutes = GetMinutesOfDay(time);
        int startMinutes = _forceCloseHour * 60 + _forceCloseMinute;
        int resumeMinutes = _resumeTradingHour * 60;

        return IsWithinWindow(currentMinutes, startMinutes, resumeMinutes);
    }

    private static int GetMinutesOfDay(DateTime time) {
        return time.Hour * 60 + time.Minute;
    }

    private static bool IsWithinWindow(int currentMinutes, int startMinutes, int endMinutes) {
        if (startMinutes == endMinutes)
            return true;

        if (startMinutes < endMinutes)
            return currentMinutes >= startMinutes && currentMinutes < endMinutes;

        return currentMinutes >= startMinutes || currentMinutes < endMinutes;
    }

    private bool IsInNewsBlackout(DateTime time) {
        foreach (NewsBlackoutWindow window in _newsBlackoutWindows) {
            if (time >= window.Start && time < window.End)
                return true;
        }

        return false;
    }

    private static List<NewsBlackoutWindow> ParseNewsBlackoutWindows(string value) {
        var windows = new List<NewsBlackoutWindow>();

        if (string.IsNullOrWhiteSpace(value))
            return windows;

        string[] entries = value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string entry in entries) {
            string[] range = entry.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);

            if (range.Length != 2)
                continue;

            if (!TryParseBlackoutTime(range[0], out DateTime start))
                continue;

            if (!TryParseBlackoutTime(range[1], out DateTime end))
                continue;

            if (end <= start)
                continue;

            windows.Add(new NewsBlackoutWindow { Start = start, End = end });
        }

        return windows;
    }

    private static bool TryParseBlackoutTime(string value, out DateTime time) {
        string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss" };

        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (!IsStrategyPosition(args.Position))
            return;

        WriteCloseCsvRecord(args.Position, args.Reason);
        _tp1States.Remove(args.Position.Id);
    }

    private void OnPositionOpened(PositionOpenedEventArgs args) {
        if (args == null || args.Position == null)
            return;

        if (!_pendingPlansByLabel.TryGetValue(args.Position.Label, out PdhpdlOrderPlan plan))
            return;

        if (_pendingCsvIdsByLabel.TryGetValue(args.Position.Label, out string csvId)) {
            _positionCsvIds[args.Position.Id] = csvId;
            _pendingCsvIdsByLabel.Remove(args.Position.Label);
        }

        RegisterTp1State(args.Position, plan);
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

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(LabelPrefix + "_");
    }

    private string GetCloseRecordId(string csvId, PositionCloseReason reason) {
        if (reason == PositionCloseReason.TakeProfit)
            return $"{csvId}-TP2";

        return $"{csvId}-{GetCloseReasonCode(reason)}";
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

    private void RegisterTp1State(Position position, PdhpdlOrderPlan plan) {
        if (position == null)
            return;

        _tp1States[position.Id] = new Tp1State {
            TargetPrice = plan.Tp1Price, CloseVolumeInUnits = plan.Tp1CloseVolumeInUnits, IsClosed = false
        };
    }

    private void TryCloseTp1(Position position) {
        if (!_tp1States.TryGetValue(position.Id, out Tp1State state))
            return;

        if (state.IsClosed)
            return;

        if (!IsTp1Reached(position, state.TargetPrice))
            return;

        double closeVolume = Math.Min(state.CloseVolumeInUnits, position.VolumeInUnits);
        closeVolume = _symbol.NormalizeVolumeInUnits(closeVolume, RoundingMode.Down);

        if (closeVolume < _symbol.VolumeInUnitsMin) {
            _robot.Print("*****TP1 skipped | Close volume too small. Position: {0}, CloseVolume: {1}", position.Id, closeVolume);
            state.IsClosed = true;
            return;
        }

        TradeResult result = _robot.ClosePosition(position, closeVolume);

        if (!result.IsSuccessful) {
            _robot.Print("*****TP1 partial close failed | Position: {0}, Error: {1}", position.Id, result.Error);
            return;
        }

        state.IsClosed = true;
        WriteTp1CsvRecord(position, closeVolume);
        _robot.Print("*****TP1 partial close succeeded | Position: {0}, Volume: {1}", position.Id, closeVolume);
    }

    private static bool IsTp1Reached(Position position, double targetPrice) {
        if (position.TradeType == TradeType.Buy)
            return position.CurrentPrice >= targetPrice;

        return position.CurrentPrice <= targetPrice;
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

        double tp1CloseVolumeInUnits = _symbol.NormalizeVolumeInUnits(totalVolumeInUnits / 2.0, RoundingMode.Down);

        if (tp1CloseVolumeInUnits < _symbol.VolumeInUnitsMin) {
            plan.RejectReason = $"TP1 close volume is too small. Tp1CloseVolume={tp1CloseVolumeInUnits}, Min={_symbol.VolumeInUnitsMin}";
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
        plan.Tp1Price = tp1;
        plan.Tp2Price = tp2;
        plan.RiskPrice = riskPrice;
        plan.StopLossPips = stopLossPips;
        plan.Tp1Pips = tp1Pips;
        plan.Tp2Pips = tp2Pips;
        plan.TotalLots = totalVolumeInUnits / _symbol.LotSize;
        plan.TotalVolumeInUnits = totalVolumeInUnits;
        plan.NativeRiskVolumeInUnits = nativeRiskVolumeInUnits;
        plan.PriceRiskCappedVolumeInUnits = priceRiskCappedVolumeInUnits;
        plan.Tp1CloseVolumeInUnits = tp1CloseVolumeInUnits;
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
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TP1: {4}, TP2: {5}, RiskPrice: {6}, StopLossPips: {7}, RiskMoney: {8}, EstimatedRiskMoney: {9}, NativeVolumeUnits: {10}, PriceRiskCappedVolumeUnits: {11}, Lots: {12}, TotalVolumeUnits: {13}, Tp1CloseVolumeUnits: {14}",
            plan.TradeType, plan.EntryMode, plan.EntryPrice, plan.StopPrice, plan.Tp1Price, plan.Tp2Price, plan.RiskPrice,
            plan.StopLossPips, plan.RiskMoney, plan.EstimatedRiskMoney, plan.NativeRiskVolumeInUnits, plan.PriceRiskCappedVolumeInUnits,
            plan.TotalLots, plan.TotalVolumeInUnits, plan.Tp1CloseVolumeInUnits);

        TradeResult result = ExecuteOrder(plan);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return;
        }

        _robot.Print("*****Order submitted | Label: {0}", plan.Label);

        if (_entryMode == PdhpdlEntryMode.Close) {
            RegisterTp1State(result.Position, plan);
            WriteEntryCsvRecord(plan, result.Position);
        } else {
            _pendingPlansByLabel[plan.Label] = plan;
            WritePendingEntryCsvRecord(plan, result.PendingOrder);
        }
    }

    private TradeResult ExecuteOrder(PdhpdlOrderPlan plan) {
        if (_entryMode == PdhpdlEntryMode.Close) {
            return _robot.ExecuteMarketOrder(plan.TradeType, _symbolName, plan.TotalVolumeInUnits, plan.Label, plan.StopLossPips,
                plan.Tp2Pips, EntryComment);
        }

        return _robot.PlaceLimitOrder(plan.TradeType, _symbolName, plan.TotalVolumeInUnits, plan.EntryPrice, plan.Label, plan.StopLossPips,
            plan.Tp2Pips, ProtectionType.Relative, null, EntryComment);
    }

    private void WriteEntryCsvRecord(PdhpdlOrderPlan plan, Position position) {
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
                Comment = "ENTRY",
                Symbol = _symbolName,
                TimeFrame = _timeFrame,
                EntryTime = position.EntryTime,
                EntryPrice = position.EntryPrice,
                StopPrice = position.StopLoss ?? plan.StopPrice,
                Tp1Price = plan.Tp1Price,
                Tp2Price = plan.Tp2Price,
                RiskPrice = Math.Abs(position.EntryPrice - (position.StopLoss ?? plan.StopPrice)),
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

    private void WritePendingEntryCsvRecord(PdhpdlOrderPlan plan, PendingOrder order) {
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
                Comment = "ENTRY",
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
                Id = GetCloseRecordId(csvId, reason),
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

    private void WriteTp1CsvRecord(Position position, double closeVolumeInUnits) {
        try {
            string csvId = GetPositionCsvId(position);

            var record = new PdhpdlTradeCsvRecord {
                Id = $"{csvId}-TP1",
                Side = position.TradeType == TradeType.Buy ? "B" : "S",
                KeyLevel = "",
                Signal = "partial-close",
                CloseEntryResult = "",
                Pullback25Result = "",
                Pullback382Result = "",
                Pullback50Result = "",
                Comment = "TP1部分止盈",
                Symbol = _symbolName,
                TimeFrame = _timeFrame,
                EntryTime = position.EntryTime,
                EntryPrice = position.EntryPrice,
                StopPrice = 0.0,
                Tp1Price = 0.0,
                Tp2Price = 0.0,
                RiskPrice = 0.0,
                VolumeInUnits = closeVolumeInUnits,
                CloseReason = "TP1",
                ProfitLoss = position.NetProfit,
                CloseTime = _robot.Server.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                PositionId = position.Id.ToString(),
                DealId = GetCloseDealId(position)
            };

            _csvLogger.Append(record);
            _robot.Print("*****CSV TP1 record added. Id: {0}, Volume: {1}", record.Id, closeVolumeInUnits);
        } catch (Exception ex) {
            _robot.Print("*****CSV TP1 write failed | {0}", ex.Message);
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

    private class NewsBlackoutWindow {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }
    }
}
