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

    // 均线来源周期上最后一根已收盘的 K 线。
    public int ConfirmedIndex => SourceBars.Count - 2;

    // 往前 barsAgo 根已收 K 线时的快慢线开口（快 - 慢）。barsAgo = 0 就是当前那根。
    public bool TryGetGap(int barsAgo, out double gap) {
        gap = double.NaN;
        int barIndex = ConfirmedIndex - barsAgo;

        if (barIndex < 0)
            return false;

        double fastRma = FastValues[barIndex];
        double slowRma = SlowValues[barIndex];

        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) || double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            return false;

        gap = fastRma - slowRma;
        return true;
    }

    public bool TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma) {
        sourceBarTime = DateTime.MinValue;
        fastRma = double.NaN;
        slowRma = double.NaN;

        int confirmedIndex = ConfirmedIndex;
        if (confirmedIndex < 0)
            return false;

        fastRma = FastValues[confirmedIndex];
        slowRma = SlowValues[confirmedIndex];
        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) || double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            return false;

        sourceBarTime = SourceBars.OpenTimes[confirmedIndex];
        return true;
    }

    private static TimeFrame ToTimeFrame(TimeFrameSelectModel minutes) {
        return minutes switch {
            TimeFrameSelectModel.M1 => TimeFrame.Minute,
            TimeFrameSelectModel.M2 => TimeFrame.Minute2,
            TimeFrameSelectModel.M3 => TimeFrame.Minute3,
            TimeFrameSelectModel.M4 => TimeFrame.Minute4,
            TimeFrameSelectModel.M5 => TimeFrame.Minute5,
            TimeFrameSelectModel.M10 => TimeFrame.Minute10,
            TimeFrameSelectModel.M15 => TimeFrame.Minute15,
            TimeFrameSelectModel.M20 => TimeFrame.Minute20,
            TimeFrameSelectModel.M30 => TimeFrame.Minute30,
            TimeFrameSelectModel.M45 => TimeFrame.Minute45,
            TimeFrameSelectModel.H1 => TimeFrame.Hour,
            TimeFrameSelectModel.H2 => TimeFrame.Hour2,
            TimeFrameSelectModel.H3 => TimeFrame.Hour3,
            TimeFrameSelectModel.H4 => TimeFrame.Hour4,
            TimeFrameSelectModel.H6 => TimeFrame.Hour6,
            TimeFrameSelectModel.H8 => TimeFrame.Hour8,
            TimeFrameSelectModel.H12 => TimeFrame.Hour12,
            TimeFrameSelectModel.D1 => TimeFrame.Daily,
            _ => throw new ArgumentOutOfRangeException(nameof(minutes), minutes, null)
        };
    }
}
