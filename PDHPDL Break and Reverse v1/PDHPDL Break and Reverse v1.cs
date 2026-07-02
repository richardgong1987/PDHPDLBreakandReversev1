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

    [Parameter("止损偏移点数", DefaultValue = 15, MinValue = 0, MaxValue = 1000)]
    public int StopOffsetTicks { get; set; }

    [Parameter("第一止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double Tp1R { get; set; }

    [Parameter("第二止盈目标", DefaultValue = 4.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double Tp2R { get; set; }

    [Parameter("回撤开仓模式", DefaultValue = PdhpdlEntryMode.Close)]
    public PdhpdlEntryMode EntryMode { get; set; }


    private PdhpdlLines _pdhpdlLines;
    private Bars _dailyBars;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;

    protected override void OnStart() {
        int daysToDraw = PdhpdlUtils.GetDaysToDraw(Bars);
        _pdhpdlLines = new PdhpdlLines(Chart, MarketData, SymbolName, daysToDraw, LineThickness);
        _dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);

        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _pdhpdlLines.Draw();

        _csvLogger = new PdhpdlTradeCsvLogger();
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        _orderExecutor = new PdhpdlOrderExecutor(this, Symbol, SymbolName, Bars.TimeFrame.ToString(), RiskPct, StopOffsetTicks, Tp1R, Tp2R,
            EntryMode, _csvLogger);

        Print("*****PDH/PDL step painter started. DaysToDraw: {0}", daysToDraw);
    }

    protected override void OnBar() {
        _pdhpdlLines.Draw();
        DetectFalseBreakoutOnClosedBar();
    }

    protected override void OnTick() { }


    private void DetectFalseBreakoutOnClosedBar() {
        PdhpdlSignal signal = PdhpdlUtils.DetectFalseBreakoutOnClosedBar(Bars, _dailyBars);

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
