using System;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlUtils {
    public static int GetDaysToDraw(Bars chartBars) {
        if (chartBars.Count < 2)
            return 2;
        DateTime chartStartDate = chartBars.OpenTimes[0].Date;
        DateTime chartEndDate = chartBars.OpenTimes[chartBars.Count - 1].Date;

        int days = (chartEndDate - chartStartDate).Days;
        return Math.Max(2, days + 2);
    }

    public static bool IsSortSignal(double high, double low, double open, double close, double pdh, double pdl) {
        return IsFalseBreakUp(high, close, pdh, open);
    }

    public static bool IsLongSignal(double high, double low, double open, double close, double pdh, double pdl) {
        return IsFalseBreakDown(low, close, pdl, open);
    }

    private static bool IsFalseBreakUp(double high, double close, double pdh, double open) {
        return high > pdh && Math.Max(open, close) < pdh;
    }


    private static bool IsFalseBreakDown(double low, double close, double pdl, double open) {
        return low < pdl && Math.Min(open, close) > pdl;
    }

    private static bool TryGetPreviousDayLevels(Bars dailyBars, out double pdh, out double pdl) {
        pdh = double.NaN;
        pdl = double.NaN;

        if (dailyBars == null || dailyBars.Count < 2)
            return false;

        int previousDailyIndex = dailyBars.Count - 2;

        pdh = dailyBars.HighPrices[previousDailyIndex];
        pdl = dailyBars.LowPrices[previousDailyIndex];

        return true;
    }

    public static PdhpdlSignal DetectFalseBreakoutOnClosedBar(Bars bars, Bars dailyBars) {
        PdhpdlSignal signal = new();

        if (bars.Count < 2)
            return signal;

        bool hasLevels = TryGetPreviousDayLevels(dailyBars, out double pdh, out double pdl);

        if (!hasLevels)
            return signal;

        int closedBarIndex = bars.Count - 2;

        DateTime barTime = bars.OpenTimes[closedBarIndex];
        double high = bars.HighPrices[closedBarIndex];
        double low = bars.LowPrices[closedBarIndex];
        double close = bars.ClosePrices[closedBarIndex];
        double open = bars.OpenPrices[closedBarIndex];

        bool shortSignal = IsSortSignal(high, low, open, close, pdh, pdl);
        bool longSignal = IsLongSignal(high, low, open, close, pdh, pdl);

        signal.HasData = true;
        signal.BarIndex = closedBarIndex;
        signal.BarTime = barTime;
        signal.High = high;
        signal.Low = low;
        signal.Close = close;
        signal.Pdh = pdh;
        signal.Pdl = pdl;
        signal.IsLongSignal = longSignal;
        signal.IsShortSignal = shortSignal;

        return signal;
    }
}
