namespace cAlgo.Robots;

public class Utils {
    public static bool CanA(PdhpdlSignalModel signalModel) {
        return signalModel.Strategy == StrategyModel.AB || StrategyModel.A == signalModel.Strategy;
    }

    public static bool CanB(PdhpdlSignalModel signalModel) {
        return signalModel.Strategy == StrategyModel.AB || StrategyModel.B == signalModel.Strategy;
    }

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
