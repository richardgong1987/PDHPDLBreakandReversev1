using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class DualRmaSeries {
    private readonly MovingAverage _fastMa;
    private readonly MovingAverage _slowMa;

    public DualRmaSeries(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, Bars chartBars,
        DualRmaLinesConfigModel config) {
        Source = config.Source;
        SourceBars = config.Source == MovingAverageSourceModel.ChartTimeFrame
            ? chartBars
            : marketData.GetBars(ToTimeFrame(config.HigherTimeFrameMinutes), symbolName);

        _fastMa = indicators.MovingAverage(SourceBars.ClosePrices, config.FastPeriod, MovingAverageType.WilderSmoothing);
        _slowMa = indicators.MovingAverage(SourceBars.ClosePrices, config.SlowPeriod, MovingAverageType.WilderSmoothing);
    }

    public MovingAverageSourceModel Source { get; }

    public Bars SourceBars { get; }

    public IndicatorDataSeries FastValues => _fastMa.Result;

    public IndicatorDataSeries SlowValues => _slowMa.Result;

    public bool TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma) {
        sourceBarTime = DateTime.MinValue;
        fastRma = double.NaN;
        slowRma = double.NaN;

        int confirmedIndex = SourceBars.Count - 2;
        if (confirmedIndex < 0)
            return false;

        fastRma = FastValues[confirmedIndex];
        slowRma = SlowValues[confirmedIndex];
        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) ||
            double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            return false;

        sourceBarTime = SourceBars.OpenTimes[confirmedIndex];
        return true;
    }

    private static TimeFrame ToTimeFrame(int minutes) {
        return minutes switch {
            1 => TimeFrame.Minute,
            2 => TimeFrame.Minute2,
            3 => TimeFrame.Minute3,
            4 => TimeFrame.Minute4,
            5 => TimeFrame.Minute5,
            10 => TimeFrame.Minute10,
            15 => TimeFrame.Minute15,
            20 => TimeFrame.Minute20,
            30 => TimeFrame.Minute30,
            45 => TimeFrame.Minute45,
            60 => TimeFrame.Hour,
            120 => TimeFrame.Hour2,
            180 => TimeFrame.Hour3,
            240 => TimeFrame.Hour4,
            360 => TimeFrame.Hour6,
            480 => TimeFrame.Hour8,
            720 => TimeFrame.Hour12,
            1440 => TimeFrame.Daily,
            _ => throw new ArgumentOutOfRangeException(nameof(minutes), minutes,
                "Unsupported higher-timeframe minutes for DualRmaSeries.")
        };
    }
}
