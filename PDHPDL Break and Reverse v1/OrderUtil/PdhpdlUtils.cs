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
        // 1. K线在PDH下方，当日第一次触及PDH，并且收线价格低于PDH，收线进场开空单。
        bool cond1 = high > pdh && pdh > Math.Max(open, close);
        // 2. K线在PDL下方，当日第一次触及PDL，并且收线价格低于PDL，收线进场开空单。
        bool cond2 = high > pdl && pdl > Math.Max(open, close);
        return cond1 || cond2;
    }

    public static bool IsLongSignal(double high, double low, double open, double close, double pdh, double pdl) {
        // - 1. K线在PDH上方，当日第一次触及PDH，并且收线价格高于PDH，收线进场开多单。
        bool cond1 = pdh > low && Math.Min(open, close) > pdh;
        //- 2.  K线在PDL上方，当日第一次触及PDL，并且收线价格高于PDL，收线进场开多单。
        bool cond2 = pdl > low && Math.Min(open, close) > pdl;
        return cond1 || cond2;
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
