using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = true)]
public class PDHPDLBreakandReversev1 : Robot {
    [Parameter("线的粗细度", DefaultValue = 3)] public int LineThickness { get; set; }

    [Parameter("展示调试日志", DefaultValue = false)]
    public bool ShowDebugLogs { get; set; }

    [Parameter("每笔交易风险百分比，默认1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1)]
    public double RiskPct { get; set; }

    [Parameter("风险安全系数", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0, Step = 0.05)]
    public double RiskSafetyFactor { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 15, MinValue = 0, MaxValue = 1000)]
    public int StopOffsetTicks { get; set; }

    [Parameter("最小止损价格距离", DefaultValue = 5.0, MinValue = 0.0, Step = 0.1)]
    public double MinRiskPrice { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double TakeProfitR { get; set; }

    [Parameter("回撤开仓模式", DefaultValue = PdhpdlEntryMode.Close)]
    public PdhpdlEntryMode EntryMode { get; set; }

    [Parameter("禁止开仓开始小时", DefaultValue = 4, MinValue = 0, MaxValue = 23)]
    public int NoNewOrdersStartHour { get; set; }

    [Parameter("强制平仓小时", DefaultValue = 4, MinValue = 0, MaxValue = 23)]
    public int ForceCloseHour { get; set; }

    [Parameter("强制平仓分钟", DefaultValue = 30, MinValue = 0, MaxValue = 59)]
    public int ForceCloseMinute { get; set; }

    [Parameter("恢复开仓小时", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
    public int ResumeTradingHour { get; set; }

    [Parameter("周五禁止开仓开始小时", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
    public int FridayNoNewOrdersStartHour { get; set; }

    [Parameter("周五强制平仓小时", DefaultValue = 3, MinValue = 0, MaxValue = 23)]
    public int FridayForceCloseHour { get; set; }

    [Parameter("周五强制平仓分钟", DefaultValue = 30, MinValue = 0, MaxValue = 59)]
    public int FridayForceCloseMinute { get; set; }

    [Parameter("五星数据空仓时间段", DefaultValue = "")]
    public string NewsBlackoutWindows { get; set; }


    private PdhpdlLines _pdhpdlLines;
    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;

    protected override void OnStart() {
        _pdhpdlLines = new PdhpdlLines(Chart, MarketData, SymbolName, Bars, LineThickness);
        _pdhpdlLines.Draw();

        Bars dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);
        _signalDetector = new PdhpdlSignalDetector(Bars, dailyBars);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger();
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard(BuildRiskGuardConfig());
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbol(Symbol), riskGuard, StopOffsetTicks, TakeProfitR, EntryMode, RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), planner, riskGuard, _csvLogger);

        Print("*****PDH/PDL Break and Reverse started.");
    }

    private PdhpdlRiskGuardConfig BuildRiskGuardConfig() {
        return new PdhpdlRiskGuardConfig {
            RiskSafetyFactor = RiskSafetyFactor,
            MinRiskPrice = MinRiskPrice,
            NoNewOrdersStartHour = NoNewOrdersStartHour,
            ForceCloseHour = ForceCloseHour,
            ForceCloseMinute = ForceCloseMinute,
            ResumeTradingHour = ResumeTradingHour,
            FridayNoNewOrdersStartHour = FridayNoNewOrdersStartHour,
            FridayForceCloseHour = FridayForceCloseHour,
            FridayForceCloseMinute = FridayForceCloseMinute,
            NewsBlackoutWindows = NewsBlackoutWindows
        };
    }

    protected override void OnBar() {
        _pdhpdlLines.Draw();
        _orderExecutor?.ManageOpenPositions();
        HandleClosedBarSignal();
    }

    protected override void OnTick() {
        _orderExecutor?.ManageOpenPositions();
    }

    private void HandleClosedBarSignal() {
        PdhpdlSignal signal = _signalDetector.DetectOnClosedBar();

        if (!signal.HasData)
            return;

        if (ShowDebugLogs) {
            Print("*****Bar closed | Time: {0}, High: {1}, Low: {2}, Close: {3}, PDH: {4}, PDL: {5}", signal.BarTime, signal.High,
                signal.Low, signal.Close, signal.Pdh, signal.Pdl);
        }

        if (signal.IsLongSignal) {
            Print("*****LONG trigger | Time: {0}, Low: {1}, Close: {2}, PDL: {3}", signal.BarTime, signal.Low, signal.Close, signal.Pdl);
        }

        if (signal.IsShortSignal) {
            Print("*****SHORT trigger | Time: {0}, High: {1}, Close: {2}, PDH: {3}", signal.BarTime, signal.High, signal.Close, signal.Pdh);
        }

        _signalMarkers.Draw(signal);
        _orderExecutor.ExecuteIfSignal(signal);
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
        _orderExecutor?.Stop();
        _signalMarkers?.Clear();
        _pdhpdlLines?.Clear();
    }
}
