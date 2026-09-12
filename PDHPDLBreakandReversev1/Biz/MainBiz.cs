using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier,
        PivotEntryGate entryGate, ConsecutiveLossCounter lossCounter) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);

        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier, entryGate, lossCounter);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier, entryGate, lossCounter);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier, PivotEntryGate entryGate, ConsecutiveLossCounter lossCounter) {
        if (signalModel.BuyOrSellOnly == BuyOrSellOnlyModel.BuyOnly) {
            return false;
        }

        if (!signalModel.HasRmaData)
            return false;

        if (!IsGapExpanding(signalModel, SignalSideModel.Sell))
            return false;


        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 蓝线下面，作空。这里判断的是如果蓝线在上面，不作
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
        if (!MatchesShortPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 没连亏到 Nlock 笔就照常放行，不查结构点。
        if (!lossCounter.IsPivotGateRequired)
            return true;

        // 连亏之后收紧：每一笔作空都要吃掉一个新的 LL，MarketStructure 最后标出的必须是 LL
        //（LH 不算），而且要是上一笔作空之后才新出的那一个。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Short, signalModel.LatestPivot, signalModel.PivotCount);
    }

    // 闸门读的是 3 根窗口那一列；1 根窗口那一列只写进 CSV 供对比，不参与判断。
    private static bool IsGapExpanding(PdhpdlSignalModel signalModel, SignalSideModel side) {
        return GapXGate.IsExpanding(signalModel.UseGapX, signalModel.GapExpansionX3Bar, signalModel.GapXThreshold, side);
    }

    private static bool MatchesShortPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
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
        CandleModel previous, CandleModel earlier, PivotEntryGate entryGate, ConsecutiveLossCounter lossCounter) {
        if (signalModel.BuyOrSellOnly == BuyOrSellOnlyModel.SellOnly) {
            return false;
        }

        if (!signalModel.HasRmaData)
            return false;

        if (!IsGapExpanding(signalModel, SignalSideModel.Buy))
            return false;

        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 条件是：蓝线在上面，作多。这里判断的是蓝线在下面，直接不作
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

        if (!MatchesLongPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 没连亏到 Nlock 笔就照常放行，不查结构点。
        if (!lossCounter.IsPivotGateRequired)
            return true;

        // 连亏之后收紧：每一笔作多都要吃掉一个新的 HH，MarketStructure 最后标出的必须是 HH
        //（HL 不算），而且要是上一笔作多之后才新出的那一个。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Long, signalModel.LatestPivot, signalModel.PivotCount);
    }

    private static bool MatchesLongPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
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
