namespace cAlgo.Robots;

public class Utils {
    public static bool AnyBarTouchesLevel(double level, params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (TouchesLevel(candle, level))
                return true;
        }

        return false;
    }

    private static bool TouchesLevel(CandleModel candle, double level) {
        return candle.Low <= level && candle.High >= level;
    }

    public static bool AnyBarIsLong(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBullish) {
                return false;
            }
        }

        return true;
    }

    public static bool AnyBarIsShort(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBearish) {
                return false;
            }
        }

        return true;
    }

    // 「策略模式」按收盘价与双 RMA 的排列，决定该方向的信号是否放行。
    // 每个分支都自带排列的方向判断，不依赖调用方是否已经做过 RMA 方向过滤。
    public static bool IsStrategyModeSatisfied(PdhpdlSignalModel signalModel, CandleModel current, SignalSideModel side) {
        return IsStrongTrend(signalModel, current, side);
    }

    private static bool IsStrongTrend(PdhpdlSignalModel signalModel, CandleModel current, SignalSideModel side) {
        // 强多头：K线收盘价格>RMA13>RMA55
        if (side == SignalSideModel.Buy) {
            return current.Close > signalModel.FastRma && signalModel.FastRma > signalModel.SlowRma;
        }

        // 强空头：K线收盘价格<RMA13<RMA55
        if (side == SignalSideModel.Sell) {
            return current.Close < signalModel.FastRma && signalModel.FastRma < signalModel.SlowRma;
        }

        return false;
    }
}
