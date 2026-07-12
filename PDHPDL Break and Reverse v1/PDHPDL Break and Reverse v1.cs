using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = true)]
public class PDHPDLBreakandReversev1 : Robot {
    [Parameter("策略模式", DefaultValue = StrategyModel.AB)]
    public StrategyModel Strategy { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = true)]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("每笔交易风险百分比，默认1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1)]
    public double RiskPct { get; set; }

    [Parameter("风险安全系数", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0, Step = 0.05)]
    public double RiskSafetyFactor { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 1000)]
    public int StopOffsetTicks { get; set; }

    [Parameter("最小止损价格距离", DefaultValue = 5.0, MinValue = 0.0, Step = 0.1)]
    public double MinRiskPrice { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double TakeProfitR { get; set; }

    [Parameter("回撤开仓模式", DefaultValue = PdhpdlEntryModel.Close)]
    public PdhpdlEntryModel EntryModel { get; set; }

    [Parameter("周六强制平仓小时（日本时间）", DefaultValue = 5, MinValue = 0, MaxValue = 23)]
    public int SaturdayForceCloseHour { get; set; }

    [Parameter("周六强制平仓分钟（日本时间）", DefaultValue = 30, MinValue = 0, MaxValue = 59)]
    public int SaturdayForceCloseMinute { get; set; }

    [Parameter("五星数据空仓时间段", DefaultValue = "")]
    public string NewsBlackoutWindows { get; set; }

    [Parameter("展示调试日志", DefaultValue = false)]
    public bool ShowDebugLogs { get; set; }

    [Parameter("debug调试", DefaultValue = false)]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "pdhpdl-trades.csv")]
    public string FileName { get; set; }

    private PdhpdlLines _pdhpdlLines;
    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;

    protected override void OnStart() {
        if (IsDebug) {
            bool result = System.Diagnostics.Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }

        _pdhpdlLines = new PdhpdlLines(Chart, MarketData, SymbolName, Bars, 3);
        _pdhpdlLines.Draw();

        Bars dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);
        _signalDetector = new PdhpdlSignalDetector(Bars, dailyBars);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger(ResetTradeLogOnStart, FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard(BuildRiskGuardConfig());
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, StopOffsetTicks, TakeProfitR, EntryModel, RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), planner, riskGuard, _csvLogger);
        Print("*****PDH/PDL Break and Reverse started.");
    }

    private PdhpdlRiskGuardConfigModel BuildRiskGuardConfig() {
        return new PdhpdlRiskGuardConfigModel {
            RiskSafetyFactor = RiskSafetyFactor,
            MinRiskPrice = MinRiskPrice,
            SaturdayForceCloseHour = SaturdayForceCloseHour,
            SaturdayForceCloseMinute = SaturdayForceCloseMinute,
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
        PdhpdlSignalModel signalModel = _signalDetector.DetectOnClosedBar(Strategy);
        if (!signalModel.HasData)
            return;

        if (ShowDebugLogs) {
            Print("*****Bar closed | Time: {0}, High: {1}, Low: {2}, Close: {3}, PDH: {4}, PDL: {5}", signalModel.BarTime, signalModel.High,
                signalModel.Low, signalModel.Close, signalModel.Pdh, signalModel.Pdl);
        }

        if (signalModel.IsLongSignal) {
            Print("*****LONG trigger | Time: {0}, Low: {1}, Close: {2}, PDL: {3}", signalModel.BarTime, signalModel.Low, signalModel.Close,
                signalModel.Pdl);
        }

        if (signalModel.IsShortSignal) {
            Print("*****SHORT trigger | Time: {0}, High: {1}, Close: {2}, PDH: {3}", signalModel.BarTime, signalModel.High,
                signalModel.Close, signalModel.Pdh);
        }

        if (_orderExecutor.ExecuteIfSignal(signalModel)) {
            _signalMarkers.Draw(signalModel);
        }
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
        _orderExecutor?.Stop();
        _signalMarkers?.Clear();
        _pdhpdlLines?.Clear();
    }
}
