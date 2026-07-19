using System;

namespace cAlgo.Robots;

public class MainBiz {
    public void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);

        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (!signalModel.HasRmaData)
            return false;

        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 蓝线在上面，作多。但这里是专门作空的。所以就跳过
         */
        if (rmaPosition == RmaPositionModel.FastAboveSlow)
            return false;

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

        if (ShortEngulf(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (ShortTop(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        if (ShortHarami(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        return false;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current, CandleModel previous,
        CandleModel earlier) {
        if (scanResult.FractalTop == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous, earlier) &&
                Utils.AnyBarIsShort(current) && current.Close < signalModel.Pdh) {
                signalModel.Label = "S_Top_1";
                signalModel.SL = previous.High;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous, earlier) &&
                Utils.AnyBarIsShort(current) && current.Close < signalModel.Pdl) {
                signalModel.Label = "S_Top_2";
                signalModel.SL = previous.High;
                return true;
            }
        }

        return false;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous, earlier) &&
                current.Close < signalModel.Pdh) {
                signalModel.Label = "S_Harami_1";
                signalModel.SL = Math.Max(previous.High, current.High);
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous, earlier) &&
                current.Close < signalModel.Pdl) {
                signalModel.Label = "S_Harami_2";
                signalModel.SL = Math.Max(previous.High, current.High);
                return true;
            }
        }

        return false;
    }

    private static bool LongHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        /**
         一. 假突破/反转
            PDL开仓条件 （多单）
            K线接触到PDL
            出现看涨信号：孕线上破。
            看涨信号的收线价格一定要高于PDL

          二. 真突破/延续
              PDH开仓条件（多单）
              K线接触到PDH
             出现看涨信号：孕线上破。
             看涨信号的收线价格一定要高于PDH
         */

        if (scanResult.HaramiSingle == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous, earlier) &&
                current.Close > signalModel.Pdl) {
                signalModel.Label = "L_Harami_1";
                signalModel.SL = Math.Min(previous.Low, current.Low);
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous, earlier) &&
                current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Harami_2";
                signalModel.SL = Math.Min(previous.Low, current.Low);
                return true;
            }
        }

        return false;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous) &&
                current.Close < signalModel.Pdh) {
                signalModel.Label = "S_Eng_1";
                signalModel.SL = current.High;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous) &&
                current.Close < signalModel.Pdl) {
                signalModel.Label = "S_Eng_2";
                signalModel.SL = current.High;
                return true;
            }
        }

        return false;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Sell) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current) && current.Close < signalModel.Pdh) {
                signalModel.Label = "S_Pin_1";
                signalModel.SL = current.High;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current) && current.Close < signalModel.Pdl) {
                signalModel.Label = "S_Pin_2";
                signalModel.SL = current.High;
                return true;
            }
        }

        return false;
    }

    // Long: the qualifying bar or three-bar pattern touches a level, then the confirmation bar closes above it.
    private static bool IsLongSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (!signalModel.HasRmaData)
            return false;

        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 蓝线在下面，代表，只作空。这但这里都是作多的，所以就不走这里的逻辑了。
         */
        if (rmaPosition == RmaPositionModel.FastBelowSlow) {
            return false;
        }


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

        if (LongEngulf(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (LongBottom(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        if (LongHarami(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        return false;
    }

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.FractalBottom == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous, earlier) &&
                Utils.AnyBarIsLong(current) && current.Close > signalModel.Pdl) {
                signalModel.Label = "L_Bot_1";
                signalModel.SL = previous.Low;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous, earlier) &&
                Utils.AnyBarIsLong(current) && current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Bot_2";
                signalModel.SL = previous.Low;
                return true;
            }
        }

        return false;
    }


    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current, previous) &&
                current.Close > signalModel.Pdl) {
                signalModel.Label = "L_Eng_1";
                signalModel.SL = current.Low;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current, previous) &&
                current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Eng_2";
                signalModel.SL = current.Low;
                return true;
            }
        }

        return false;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Buy) {
            // (1).假突破/反转
            if (Utils.CanA(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdl, current) && current.Close > signalModel.Pdl) {
                signalModel.Label = "L_Pin_1";
                signalModel.SL = current.Low;
                return true;
            }

            // (2).真突破/延续
            if (Utils.CanB(signalModel) && Utils.AnyBarTouchesLevel(signalModel.Pdh, current) && current.Close > signalModel.Pdh) {
                signalModel.Label = "L_Pin_2";
                signalModel.SL = current.Low;
                return true;
            }
        }

        return false;
    }
}
