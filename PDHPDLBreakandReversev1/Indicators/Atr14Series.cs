using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class Atr14Series {
    private const int Period = 14;
    private readonly Bars _bars;
    private readonly AverageTrueRange _atr;

    public Atr14Series(IIndicatorsAccessor indicators, Bars bars) {
        _bars = bars;
        _atr = indicators.AverageTrueRange(bars, Period, MovingAverageType.WilderSmoothing);
    }

    public bool TryGetValue(int barIndex, out double atr) {
        atr = double.NaN;

        if (barIndex < Period - 1 || barIndex >= _bars.Count)
            return false;

        atr = _atr.Result[barIndex];
        return !double.IsNaN(atr) && !double.IsInfinity(atr) && atr > 0;
    }

    public bool IsBarRangeTooLarge(int barIndex, double high, double low, double maxAtrMultiple) {
        if (!TryGetValue(barIndex, out double atr))
            return false;

        return high - low > atr * maxAtrMultiple;
    }
}
