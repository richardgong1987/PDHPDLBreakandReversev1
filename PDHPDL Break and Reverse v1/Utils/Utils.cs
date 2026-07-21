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

    public static bool Strategy(PdhpdlSignalModel signalModel, CandleModel current, SignalSideModel side) {
        StrategyModel strategy = signalModel.Strategy;
        if (strategy == StrategyModel.Strong) {
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

        if (strategy == StrategyModel.Weak) {
            // 弱多头：RMA13>K线收盘价格>RMA55
            if (side == SignalSideModel.Buy) {
                return signalModel.FastRma > current.Close && current.Close > signalModel.SlowRma;
            }

            // 弱空头：RMA13<K线收盘价格<RMA55
            if (side == SignalSideModel.Sell) {
                return signalModel.FastRma < current.Close && current.Close < signalModel.SlowRma;
            }

            return false;
        }

        if (strategy == StrategyModel.StopWhenVolatility) {
            // 趋势转换或者震荡：RMA13>RMA55>K线收盘价格  （不交易） | 趋势转换或者震荡：RMA13<RMA55<K线收盘价格 （不交易）
            bool a = signalModel.FastRma > signalModel.SlowRma && signalModel.SlowRma > current.Close;
            bool b = signalModel.FastRma < signalModel.SlowRma && signalModel.SlowRma < current.Close;

            if (side == SignalSideModel.Buy) {
                return !a;
            }

            if (side == SignalSideModel.Sell) {
                return !b;
            }
        }

        return true;
    }
}
