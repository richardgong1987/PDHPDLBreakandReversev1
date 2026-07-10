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

        signalModel.IsShortSignal = IsShortSignal(signalModel);
        signalModel.IsLongSignal = IsLongSignal(signalModel);

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

    /**
     * PDH/PDL  V1

    一. 假突破/反转
    PDH开仓条件（空单）
    K线接触到PDH
    出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
    看跌信号的收线价格一定要低于PDH

    PDL开仓条件 （多单）
    K线接触到PDL
    出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
    看涨信号的收线价格一定要高于PDL


    二. 真突破/延续

    PDH开仓条件（多单）
    K线接触到PDH
    出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
    看涨信号的收线价格一定要高于PDH

    PDL开仓条件（空单）
    K线接触到PDL
    出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破
    看跌信号的收线价格一定要低于PDL


    三. 风控

    仓位：止损金额为仓位的1%
    止损：信号K线的极值点+50点的容错
    止盈：2R
     */
    public static bool IsShortSignal(PdhpdlSignalModel pdhpdlSignalModel) {
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

        CandleModel bar = new(open: pdhpdlSignalModel.Open, high: pdhpdlSignalModel.High, low: pdhpdlSignalModel.Low, close: pdhpdlSignalModel.Close);

        SignalSideModel signalSideModel = PinBarShort(pdhpdlSignalModel, bar);


        // label = null;
        // double body = Math.Max(open, close);
        // bool rejectedFromPdh = high > pdh && pdh > body;
        // bool rejectedFromPdl = high > pdl && pdl > body;
        // return rejectedFromPdh || rejectedFromPdl;
        return false;
    }

    private static SignalSideModel PinBarShort(PdhpdlSignalModel pdhpdlSignalModel, CandleModel bar) {
        SignalSideModel signalSideModel = HanJinSignals26.Pinbar(bar);
        return signalSideModel;
    }

    // Long: the bar pierced a level (PDH or PDL) but closed back above it.
    public static bool IsLongSignal(PdhpdlSignalModel pdhpdlSignalModel) {
        /**
         *
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

        // label = null;
        // double body = Math.Min(open, close);
        // bool rejectedFromPdh = pdh > low && body > pdh;
        // bool rejectedFromPdl = pdl > low && body > pdl;

        // return rejectedFromPdh || rejectedFromPdl;
        return false;
    }
}
