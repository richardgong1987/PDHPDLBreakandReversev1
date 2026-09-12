using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class Atr14Series {
    private const int Period = 14;

    // ATR 状态值的基准长度：当前 ATR 与它自己近 100 根的均值相比，>1 波动放大、<1 收缩。
    private const int RatioAveragePeriod = 100;

    private readonly Bars _bars;
    private readonly AverageTrueRange _atr;
    private readonly SimpleMovingAverage _atrAverage;

    public Atr14Series(IIndicatorsAccessor indicators, Bars bars) {
        _bars = bars;
        _atr = indicators.AverageTrueRange(bars, Period, MovingAverageType.WilderSmoothing);
        _atrAverage = indicators.SimpleMovingAverage(_atr.Result, RatioAveragePeriod);
    }

    // OnBar() 里最后一根完全收盘的 K 线。非图表周期的序列（H1、日线）也按同样口径取值，
    // 避免把正在形成的那根算进去。
    public int LastClosedBarIndex => _bars.Count - 2;

    // 数据不足时返回 NaN，由调用方决定（CSV 留空）。
    public double LastClosedValue => TryGetValue(LastClosedBarIndex, out double atr) ? atr : double.NaN;

    public double LastClosedRatio => TryGetRatio(LastClosedBarIndex, out double ratio) ? ratio : double.NaN;

    public bool TryGetValue(int barIndex, out double atr) {
        atr = double.NaN;

        if (barIndex < Period - 1 || barIndex >= _bars.Count)
            return false;

        atr = _atr.Result[barIndex];
        return IsUsable(atr);
    }

    // ATR 状态值 = 当前 ATR(14) ÷ ATR(14) 近 RatioAveragePeriod 根的均值。
    public bool TryGetRatio(int barIndex, out double ratio) {
        ratio = double.NaN;

        if (!TryGetValue(barIndex, out double atr))
            return false;

        if (barIndex < Period - 1 + RatioAveragePeriod - 1)
            return false;

        double average = _atrAverage.Result[barIndex];

        if (!IsUsable(average))
            return false;

        ratio = atr / average;
        return true;
    }

    public bool IsBarRangeTooLarge(int barIndex, double high, double low, double maxAtrMultiple) {
        if (!TryGetValue(barIndex, out double atr))
            return false;

        return high - low > atr * maxAtrMultiple;
    }

    private static bool IsUsable(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
