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

    // 上方一组关键价位：日线 PDH。
    public static PdhpdlKeyLevelModel[] PdhLevels(PdhpdlSignalModel signalModel) {
        return new[] { new PdhpdlKeyLevelModel("Pdl1", signalModel.Pdl1), new PdhpdlKeyLevelModel("Pdh1", signalModel.Pdh1), };
    }

    // 下方一组关键价位：日线 PDL。
    public static PdhpdlKeyLevelModel[] PdlLevels(PdhpdlSignalModel signalModel) {
        return new[] { new PdhpdlKeyLevelModel("Pdl1", signalModel.Pdl1), new PdhpdlKeyLevelModel("Pdh1", signalModel.Pdh1), };
    }

    // 看跌确认：K线接触到该价位，且收盘价低于该价位。返回第一个命中的价位名。
    public static bool TryFindSellKeyLevel(PdhpdlKeyLevelModel[] levels, double closePrice, CandleModel[] touchCandles,
        out string keyLevel) {
        foreach (PdhpdlKeyLevelModel level in levels) {
            if (level.IsConfigured && AnyBarTouchesLevel(level.Price, touchCandles) && closePrice < level.Price) {
                keyLevel = level.Name;
                return true;
            }
        }

        keyLevel = "";
        return false;
    }

    // 看涨确认：K线接触到该价位，且收盘价高于该价位。返回第一个命中的价位名。
    public static bool TryFindBuyKeyLevel(PdhpdlKeyLevelModel[] levels, double closePrice, CandleModel[] touchCandles,
        out string keyLevel) {
        foreach (PdhpdlKeyLevelModel level in levels) {
            if (level.IsConfigured && AnyBarTouchesLevel(level.Price, touchCandles) && closePrice > level.Price) {
                keyLevel = level.Name;
                return true;
            }
        }

        keyLevel = "";
        return false;
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
        switch (signalModel.Strategy) {
            case StrategyModel.Strong:
                return IsStrongTrend(signalModel, current, side);
            case StrategyModel.Weak:
                return IsWeakTrend(signalModel, current, side);
            case StrategyModel.StrongWeak:
                return IsStrongTrend(signalModel, current, side) || IsWeakTrend(signalModel, current, side);
            case StrategyModel.StopWhenVolatility:
                return IsVolatility(signalModel, current, side);
        }

        return true; // All：不作隔离
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

    private static bool IsWeakTrend(PdhpdlSignalModel signalModel, CandleModel current, SignalSideModel side) {
        // 弱多头：RMA13>K线收盘价格>RMA55

        // 弱空头：RMA13<K线收盘价格<RMA55
        return (signalModel.FastRma < current.Close && current.Close < signalModel.SlowRma) ||
               (signalModel.FastRma > current.Close && current.Close > signalModel.SlowRma);
    }

    // 趋势转换或者震荡：多头 RMA13>RMA55>K线收盘价格 | 空头 RMA13<RMA55<K线收盘价格，两者都不交易。
    private static bool IsVolatility(PdhpdlSignalModel signalModel, CandleModel current, SignalSideModel side) {
        if (side == SignalSideModel.Buy) {
            return signalModel.FastRma > signalModel.SlowRma && signalModel.SlowRma > current.Close;
        }

        if (side == SignalSideModel.Sell) {
            return signalModel.FastRma < signalModel.SlowRma && signalModel.SlowRma < current.Close;
        }

        return false;
    }
}
