namespace cAlgo.Robots;

public class Utils {
    // 突破分支开关（原 CanA/CanB）已迁移到 StrategyModePolicy.AllowsReversal / AllowsContinuation。

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
            if (candle.BodyDirection != 1) {
                return false;
            }
        }

        return true;
    }

    public static bool AnyBarIsShort(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (candle.BodyDirection != -1) {
                return false;
            }
        }

        return true;
    }
}
