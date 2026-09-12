using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// DMI(14)：ADX 是趋势强度（不分多空），DI+ / DI- 是多头 / 空头方向的力量。
// 三者出自同一个 cTrader 指标，所以共用一个实例。只用于写进 CSV，不参与进出场判断。
public class Dms14Series {
    private const int Period = 14;

    private readonly Bars _bars;
    private readonly DirectionalMovementSystem _dms;

    public Dms14Series(IIndicatorsAccessor indicators, Bars bars) {
        _bars = bars;
        _dms = indicators.DirectionalMovementSystem(bars, Period);
    }

    // 与 Atr14Series 同一口径：只读最后一根完全收盘的 K 线，不看正在形成的那根。
    public int LastClosedBarIndex => _bars.Count - 2;

    public double LastClosedAdx => ReadAt(_dms.ADX, LastClosedBarIndex);

    // 再往前一根已收盘 K 线的 ADX：和 LastClosedAdx 一起看，趋势强度是在增强还是在衰减。
    public double PreviousClosedAdx => ReadAt(_dms.ADX, LastClosedBarIndex - 1);

    public double LastClosedDiPlus => ReadAt(_dms.DIPlus, LastClosedBarIndex);

    public double LastClosedDiMinus => ReadAt(_dms.DIMinus, LastClosedBarIndex);

    // ADX 要经过两轮 Wilder 平滑才成形，暖机期读到的是 NaN；一律返回 NaN，由调用方留空。
    private double ReadAt(IndicatorDataSeries series, int barIndex) {
        if (barIndex < Period * 2 || barIndex >= _bars.Count)
            return double.NaN;

        double value = series[barIndex];

        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            return double.NaN;

        return value;
    }
}
