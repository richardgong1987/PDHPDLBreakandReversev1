using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier) {
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

        if (!Utils.IsStrategyModeSatisfied(signalModel, current, SignalSideModel.Sell)) {
            return false;
        }

        /*
            一. 假突破/反转
               PDH开仓条件（空单）
               K线接触到PDH
               出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
               看跌信号的收线价格一定要低于PDH
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

        if (!Utils.IsStrategyModeSatisfied(signalModel, current, SignalSideModel.Buy)) {
            return false;
        }

        /**
         一. 假突破/反转
            PDL开仓条件 （多单）
            K线接触到PDL
            出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
            看涨信号的收线价格一定要高于PDL

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

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current, CandleModel previous,
        CandleModel earlier) {
        if (scanResult.FractalTop == SignalSideModel.Sell && Utils.AnyBarIsShort(current)) {
            CandleModel[] touchCandles = { current, previous, earlier };

            // (1).假突破/反转
            if (Utils.TryFindSellKeyLevel(Utils.PdhLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "S_Top_1";
                signalModel.SL = previous.High;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle == SignalSideModel.Sell) {
            CandleModel[] touchCandles = { current, previous, earlier };

            // (1).假突破/反转
            if (Utils.TryFindSellKeyLevel(Utils.PdhLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "S_Harami_1";
                signalModel.SL = Math.Max(previous.High, current.High);
                signalModel.KeyLevel = reversalLevel;
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
         */

        if (scanResult.HaramiSingle == SignalSideModel.Buy) {
            CandleModel[] touchCandles = { current, previous, earlier };

            // (1).假突破/反转
            if (Utils.TryFindBuyKeyLevel(Utils.PdlLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "L_Harami_1";
                signalModel.SL = Math.Min(previous.Low, current.Low);
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf == SignalSideModel.Sell) {
            CandleModel[] touchCandles = { current, previous };

            // (1).假突破/反转
            if (Utils.TryFindSellKeyLevel(Utils.PdhLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "S_Eng_1";
                signalModel.SL = current.High;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Sell) {
            CandleModel[] touchCandles = { current };

            // (1).假突破/反转
            if (Utils.TryFindSellKeyLevel(Utils.PdhLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "S_Pin_1";
                signalModel.SL = current.High;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }

    // Long: the qualifying bar or three-bar pattern touches a level, then the confirmation bar closes above it.

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.FractalBottom == SignalSideModel.Buy && Utils.AnyBarIsLong(current)) {
            CandleModel[] touchCandles = { current, previous, earlier };

            // (1).假突破/反转
            if (Utils.TryFindBuyKeyLevel(Utils.PdlLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "L_Bot_1";
                signalModel.SL = previous.Low;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }


    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf == SignalSideModel.Buy) {
            CandleModel[] touchCandles = { current, previous };

            // (1).假突破/反转
            if (Utils.TryFindBuyKeyLevel(Utils.PdlLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "L_Eng_1";
                signalModel.SL = current.Low;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar == SignalSideModel.Buy) {
            CandleModel[] touchCandles = { current };

            // (1).假突破/反转
            if (Utils.TryFindBuyKeyLevel(Utils.PdlLevels(signalModel), current.Close, touchCandles, out string reversalLevel)) {
                signalModel.Label = "L_Pin_1";
                signalModel.SL = current.Low;
                signalModel.KeyLevel = reversalLevel;
                return true;
            }
        }

        return false;
    }
}
