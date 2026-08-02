using System;
using System.Diagnostics;
using System.IO;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = true)]
public class PDHPDLBreakandReversev1 : Robot {
    [Parameter("策略模式", DefaultValue = StrategyModel.All)]
    public StrategyModel Strategy { get; set; }

    [Parameter("N次止损Lock", DefaultValue = 3)]
    public int Nlock { get; set; }

    [Parameter("ATR倍数", DefaultValue = 3)] public double MaxBarRangeAtr { get; set; }


    [Parameter("启动时清空交易记录CSV", DefaultValue = false)]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("每笔交易风险百分比，默认1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1)]
    public double RiskPct { get; set; }

    [Parameter("风险安全系数", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0, Step = 0.05)]
    public double RiskSafetyFactor { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 1000)]
    public int StopOffsetTicks { get; set; }

    [Parameter("最小止损点数 (Pips)", DefaultValue = 5.0, MinValue = 0.0, Step = 0.1)]
    public double MinStopLossPips { get; set; }

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

    [Parameter("均线来源", DefaultValue = MovingAverageSourceModel.HigherTimeFrame, Group = "均线")]
    public MovingAverageSourceModel MaSource { get; set; }

    [Parameter("均线周期 RMA 1 (快)", DefaultValue = 13, MinValue = 1, Group = "均线")]
    public int MaFastPeriod { get; set; }

    [Parameter("均线周期 RMA 2 (慢)", DefaultValue = 55, MinValue = 1, Group = "均线")]
    public int MaSlowPeriod { get; set; }

    [Parameter("均线周期(分钟)", DefaultValue = 120, MinValue = 1, Group = "均线")]
    public int MaTimeFrameMinutes { get; set; }

    private PdhpdlLines _pdhpdlLines;
    private DualRmaSeries _rmaSeries;
    private DualRmaLines _movingAverageLines;
    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;
    private Atr14Series _atr14;

    protected override void OnStart() {
        LaunchDebug();
        DrawPdhPdl();
        DrawDualRmaLines();
        _atr14 = new Atr14Series(Indicators, Bars);
        Bars dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);
        _signalDetector = new PdhpdlSignalDetector(Bars, dailyBars, _rmaSeries);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard(BuildRiskGuardConfig());
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, StopOffsetTicks, TakeProfitR, EntryModel, RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), planner, riskGuard, _csvLogger);
        Print("*****PDH/PDL Break and Reverse started.");
    }

    private void DrawDualRmaLines() {
        DualRmaLinesConfigModel rmaConfig = BuildMovingAverageConfig();
        _rmaSeries = new DualRmaSeries(MarketData, Indicators, SymbolName, Bars, rmaConfig);
        _movingAverageLines = new DualRmaLines(Chart, Bars, _rmaSeries, rmaConfig.Thickness);
        _movingAverageLines.Draw();
    }

    private void DrawPdhPdl() {
        _pdhpdlLines = new PdhpdlLines(Chart, MarketData, SymbolName, Bars, 3);
        _pdhpdlLines.Draw();
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    private DualRmaLinesConfigModel BuildMovingAverageConfig() {
        return new DualRmaLinesConfigModel {
            Source = MaSource, FastPeriod = MaFastPeriod, SlowPeriod = MaSlowPeriod, HigherTimeFrameMinutes = MaTimeFrameMinutes
        };
    }

    // 输出目录按运行模式分开、互不覆盖：回测目录由脚本每次清空重建，模拟/实盘目录只追加、从不删除。
    // 回测经 run_conditions 传入绝对路径 FileName，此目录会被忽略（见 PdhpdlTradeCsvLogger）。
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    private PdhpdlRiskGuardConfigModel BuildRiskGuardConfig() {
        return new PdhpdlRiskGuardConfigModel {
            RiskSafetyFactor = RiskSafetyFactor,
            MinStopLossPips = MinStopLossPips,
            SaturdayForceCloseHour = SaturdayForceCloseHour,
            SaturdayForceCloseMinute = SaturdayForceCloseMinute,
            NewsBlackoutWindows = NewsBlackoutWindows
        };
    }

    protected override void OnBar() {
        _pdhpdlLines.Draw();
        _movingAverageLines?.Draw();
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

        signalModel.IsBigK = _atr14.IsBarRangeTooLarge(signalModel.BarIndex, signalModel.High, signalModel.Low, 3);
        signalModel.Atr = _atr14.TryGetValue(signalModel.BarIndex, out double atr) ? atr : 0.0;
        signalModel.MaxBarRangeAtr = MaxBarRangeAtr;
        signalModel.Nlock = Nlock;
        signalModel.LossCount = _orderExecutor.LossCount();

        if (_orderExecutor.ExecuteIfSignal(signalModel)) {
            _signalMarkers.Draw(signalModel);
        }
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
        _orderExecutor?.Stop();
        _signalMarkers?.Clear();
        _pdhpdlLines?.Clear();
        _movingAverageLines?.Clear();
    }
}
