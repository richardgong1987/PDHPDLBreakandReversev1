using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the last fully closed bar plus the previous day's levels and applies the
// PDH/PDL false-breakout rules. OnBar fires when a new bar opens, so the closed bar is Count - 2.
public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
    }

    public PdhpdlSignalModel DetectOnClosedBar() {
        PdhpdlSignalModel signalModel = new();

        if (_chartBars.Count < 2 || !TryGetPreviousDayLevels(out double pdh, out double pdl))
            return signalModel;


        int currentIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()
        int previousIndex = currentIndex - 1;
        int earlierIndex = currentIndex - 2;

        CandleModel current = new(open: _chartBars.OpenPrices[currentIndex], high: _chartBars.HighPrices[currentIndex],
            low: _chartBars.LowPrices[currentIndex], close: _chartBars.ClosePrices[currentIndex]);

        CandleModel previous = new(open: _chartBars.OpenPrices[previousIndex], high: _chartBars.HighPrices[previousIndex],
            low: _chartBars.LowPrices[previousIndex], close: _chartBars.ClosePrices[previousIndex]);

        CandleModel earlier = new(open: _chartBars.OpenPrices[earlierIndex], high: _chartBars.HighPrices[earlierIndex],
            low: _chartBars.LowPrices[earlierIndex], close: _chartBars.ClosePrices[earlierIndex]);

        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);

        int closedBarIndex = _chartBars.Count - 2;

        double high = _chartBars.HighPrices[closedBarIndex];
        double low = _chartBars.LowPrices[closedBarIndex];
        double open = _chartBars.OpenPrices[closedBarIndex];
        double close = _chartBars.ClosePrices[closedBarIndex];

        signalModel.HasData = true;
        signalModel.BarIndex = closedBarIndex;
        signalModel.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signalModel.Open = open;
        signalModel.Close = close;
        signalModel.High = high;
        signalModel.Low = low;
        signalModel.Pdh = pdh;
        signalModel.Pdl = pdl;

        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier);

        return signalModel;
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

    public static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        /*
            一. 假突破/反转

               PDH开仓条件（空单）
               K线接触到PDH
               出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
               看跌信号的收线价格一定要低于PDH

           二. 真突破/延续
               PDL开仓条件（空单）
               K线接触到PDL
               出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破
               看跌信号的收线价格一定要低于PDL
         */
        if (ShortPinBar(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortEngulf(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortTop(signalModel, scanResult, current, previous)) {
            return true;
        }

        return false;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.FractalTop == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (current.High > signalModel.Pdh && signalModel.Pdh > current.Close) {
                signalModel.Label = "S_Top_反转";
                signalModel.SL = previous.High;
                return true;
            }

            // (2).真突破/延续
            if (signalModel.Pdl > current.Low && signalModel.Pdl > previous.Close) {
                signalModel.Label = "S_Top_突破";
                signalModel.SL = previous.High;
                return true;
            }
        }

        return false;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (current.High > signalModel.Pdh && signalModel.Pdh > current.Close) {
                signalModel.Label = "S_Eng_反转";
                signalModel.SL = current.High;
                return true;
            }

            // (2).真突破/延续
            if (signalModel.Pdl > current.Low && signalModel.Pdl > current.Close) {
                signalModel.Label = "S_Eng_突破";
                signalModel.SL = current.High;
                return true;
            }
        }

        return false;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (current.High > signalModel.Pdh && signalModel.Pdh > current.Close) {
                signalModel.Label = "S_Pin_反转";
                signalModel.SL = current.High;
                return true;
            }

            // (2).真突破/延续
            if (signalModel.Pdl > current.Low && signalModel.Pdl > current.Close) {
                signalModel.Label = "S_Pin_突破";
                signalModel.SL = current.High;
                return true;
            }
        }

        return false;
    }

    // Long: the bar pierced a level (PDH or PDL) but closed back above it.
    public static bool IsLongSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        /**
         一. 假突破/反转
            PDL开仓条件 （多单）
            K线接触到PDL
            出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
            看涨信号的收线价格一定要高于PDL

          二. 真突破/延续
              PDH开仓条件（多单）
              K线接触到PDH
             出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
             看涨信号的收线价格一定要高于PDH
         */

        if (LongPinbar(signalModel, scanResult, current)) {
            return true;
        }

        if (LongEngulf(signalModel, scanResult, current)) {
            return true;
        }

        if (scanResult.FractalBottom == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (signalModel.Pdh > current.BodyTop) {
                signalModel.Label = "L_Top_反转";
                signalModel.SL = previous.Low;
                return true;
            }

            // (2).真突破/延续
            if (signalModel.Pdl > current.Low && signalModel.Pdl > previous.BodyTop) {
                signalModel.Label = "L_Top_突破";
                signalModel.SL = previous.Low;
                return true;
            }
        }

        return false;
    }

    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (signalModel.Pdl > current.Low && signalModel.Close > current.Low) {
                signalModel.Label = "L_Eng_反转";
                signalModel.SL = current.Low;
                return true;
            }

            // (2).真突破/延续
            if (current.High > signalModel.Pdh && current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Eng_突破";
                signalModel.SL = current.Low;
                return true;
            }
        }

        return false;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (signalModel.Pdl > current.Low && current.Close > signalModel.Pdl) {
                signalModel.Label = "L_Pin_反转";
                signalModel.SL = current.Low;
                return true;
            }

            // (2).真突破/延续
            if (current.High > signalModel.Pdh && current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Pin_突破";
                signalModel.SL = current.Low;
                return true;
            }
        }

        return false;
    }
}
