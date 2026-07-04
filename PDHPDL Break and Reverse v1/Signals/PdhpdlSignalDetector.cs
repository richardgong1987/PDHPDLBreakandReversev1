using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the last fully closed bar plus the previous day's levels and applies
// PdhpdlSignalRules. OnBar fires when a new bar opens, so the closed bar is Count - 2.
public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
    }

    public PdhpdlSignal DetectOnClosedBar() {
        PdhpdlSignal signal = new();

        if (_chartBars.Count < 2 || !TryGetPreviousDayLevels(out double pdh, out double pdl))
            return signal;

        int closedBarIndex = _chartBars.Count - 2;

        double high = _chartBars.HighPrices[closedBarIndex];
        double low = _chartBars.LowPrices[closedBarIndex];
        double open = _chartBars.OpenPrices[closedBarIndex];
        double close = _chartBars.ClosePrices[closedBarIndex];

        signal.HasData = true;
        signal.BarIndex = closedBarIndex;
        signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signal.High = high;
        signal.Low = low;
        signal.Close = close;
        signal.Pdh = pdh;
        signal.Pdl = pdl;
        signal.IsShortSignal = PdhpdlSignalRules.IsShortSignal(high, open, close, pdh, pdl);
        signal.IsLongSignal = PdhpdlSignalRules.IsLongSignal(low, open, close, pdh, pdl);

        return signal;
    }

    private bool TryGetPreviousDayLevels(out double pdh, out double pdl) {
        pdh = double.NaN;
        pdl = double.NaN;

        if (_dailyBars == null || _dailyBars.Count < 2)
            return false;

        int previousDailyIndex = _dailyBars.Count - 2;
        pdh = _dailyBars.HighPrices[previousDailyIndex];
        pdl = _dailyBars.LowPrices[previousDailyIndex];
        return true;
    }
}
