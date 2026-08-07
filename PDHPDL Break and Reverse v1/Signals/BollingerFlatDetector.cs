using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

/// <summary>
/// Reusable Bollinger Band flatness detector.
///
/// Public methods:
/// 1. IsFlat()
/// 2. DrawBollingerBands()
///
/// Call both methods from the cBot's OnBarClosed() method so that
/// the class works only with completed bars.
/// </summary>
public sealed class BollingerFlatDetector {
    private readonly Bars _bars;
    private readonly Chart _chart;
    private readonly BollingerBands _bollingerBands;
    private readonly AverageTrueRange _atr;

    private readonly int _bollingerPeriod;
    private readonly int _atrPeriod;
    private readonly int _lookbackBars;

    private readonly double _maxSlopeAtrPerBar;
    private readonly double _maxLineRangeAtr;
    private readonly double _maxWidthVariation;

    private readonly string _chartObjectPrefix;

    public BollingerFlatDetector(Bars bars, Chart chart, IIndicatorsAccessor indicators, int bollingerPeriod = 20,
        double bollingerDeviations = 2.0, int lookbackBars = 48, int atrPeriod = 14, double maxSlopeAtrPerBar = 0.02,
        double maxLineRangeAtr = 1.5, double maxWidthVariation = 0.25, string chartObjectPrefix = "BollingerFlatDetector") {
        if (bars == null)
            throw new ArgumentNullException(nameof(bars));

        if (chart == null)
            throw new ArgumentNullException(nameof(chart));

        if (indicators == null)
            throw new ArgumentNullException(nameof(indicators));

        if (bollingerPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(bollingerPeriod));

        if (bollingerDeviations <= 0)
            throw new ArgumentOutOfRangeException(nameof(bollingerDeviations));

        if (lookbackBars < 5)
            throw new ArgumentOutOfRangeException(nameof(lookbackBars));

        if (atrPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(atrPeriod));

        if (maxSlopeAtrPerBar < 0)
            throw new ArgumentOutOfRangeException(nameof(maxSlopeAtrPerBar));

        if (maxLineRangeAtr < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLineRangeAtr));

        if (maxWidthVariation < 0)
            throw new ArgumentOutOfRangeException(nameof(maxWidthVariation));

        _bars = bars;
        _chart = chart;

        _bollingerPeriod = bollingerPeriod;
        _atrPeriod = atrPeriod;
        _lookbackBars = lookbackBars;

        _maxSlopeAtrPerBar = maxSlopeAtrPerBar;
        _maxLineRangeAtr = maxLineRangeAtr;
        _maxWidthVariation = maxWidthVariation;

        _chartObjectPrefix = string.IsNullOrWhiteSpace(chartObjectPrefix) ? "BollingerFlatDetector" : chartObjectPrefix;

        _bollingerBands = indicators.BollingerBands(_bars.ClosePrices, bollingerPeriod, bollingerDeviations, MovingAverageType.Simple);

        _atr = indicators.AverageTrueRange(atrPeriod, MovingAverageType.Exponential);
    }

    /// <summary>
    /// Returns true only when all three tests pass:
    ///
    /// 1. The three regression slopes are almost flat.
    /// 2. The three lines did not bend too much.
    /// 3. The distance between Top and Bottom stayed stable.
    ///
    /// Returns false when there is not enough historical data.
    /// </summary>
    public bool IsFlat() {
        if (!HasEnoughData())
            return false;


        /*
         * FORMULA 1: Average ATR
         *
         *                  ATR(0) + ATR(1) + ... + ATR(N - 1)
         * AverageATR =     ------------------------------------
         *                                      N
         *
         * N = _lookbackBars.
         *
         * We use the average ATR as our measuring ruler.
         * This lets the same rules work more fairly on markets
         * with very different prices, such as EURUSD, gold, and BTC.
         */
        double averageAtr = GetAverage(_atr.Result, _lookbackBars);

        if (!IsPositiveFiniteNumber(averageAtr))
            return false;

        /*
       //             * TEST 1: Overall direction
       //             *
       //             * First calculate the raw linear regression slope:
       //             *
       //             *            SUM((x_i - MeanX) * (y_i - MeanY))
       //             * RawSlope = -----------------------------------------
       //             *                    SUM((x_i - MeanX)^2)
       //             *
       //             * x_i = the bar number: 0, 1, 2, ..., N - 1.
       //             * y_i = the Bollinger line value at that bar.
       //             *
       //             * RawSlope means:
       //             * "How much does this line move for each new bar?"
       //             *
       //             * Then normalize the slope with ATR:
       //             *
       //             * NormalizedSlope = RawSlope / AverageATR
       //             *
       //             * Example:
       //             * RawSlope = 0.2
       //             * AverageATR = 20
       //             * NormalizedSlope = 0.2 / 20 = 0.01 ATR per bar
       //             */
        double topSlopeAtr = GetLinearRegressionSlope(_bollingerBands.Top, _lookbackBars) / averageAtr;

        double middleSlopeAtr = GetLinearRegressionSlope(_bollingerBands.Main, _lookbackBars) / averageAtr;

        double bottomSlopeAtr = GetLinearRegressionSlope(_bollingerBands.Bottom, _lookbackBars) / averageAtr;
        /*
         * A line passes when:
         *
         * ABS(NormalizedSlope) <= MaximumAllowedSlope
         *
         * Math.Abs() treats upward and downward movement equally:
         * ABS(+0.03) = 0.03
         * ABS(-0.03) = 0.03
         *
         * All three lines must pass.
         */
        bool slopesAreFlat = Math.Abs(topSlopeAtr) <= _maxSlopeAtrPerBar && Math.Abs(middleSlopeAtr) <= _maxSlopeAtrPerBar &&
                             Math.Abs(bottomSlopeAtr) <= _maxSlopeAtrPerBar;

        /*
       //             * TEST 2: Large hills or valleys
       //             *
       //             * A line may go up and then come back down. Its slope can
       //             * be close to zero even though the line is clearly not flat.
       //             * Therefore, we also measure its complete movement range:
       //             *
       //             * LineRange = HighestLineValue - LowestLineValue
       //             *
       //             * LineRangeAtr = LineRange / AverageATR
       //             *
       //             * Example:
       //             * Highest = 4065
       //             * Lowest = 4045
       //             * AverageATR = 20
       //             * LineRangeAtr = (4065 - 4045) / 20 = 1 ATR
       //             */
        double topRangeAtr = GetRange(_bollingerBands.Top, _lookbackBars) / averageAtr;

        double middleRangeAtr = GetRange(_bollingerBands.Main, _lookbackBars) / averageAtr;

        double bottomRangeAtr = GetRange(_bollingerBands.Bottom, _lookbackBars) / averageAtr;

        /*
         * A line passes when:
         *
         * LineRangeAtr <= MaximumAllowedLineRangeAtr
         *
         * All three lines must pass.
         */
        bool linesDoNotBendTooMuch = topRangeAtr <= _maxLineRangeAtr && middleRangeAtr <= _maxLineRangeAtr &&
                                     bottomRangeAtr <= _maxLineRangeAtr;

        /*
       //             * TEST 3: Changing distance between Top and Bottom
       //             *
       //             * For every bar:
       //             *
       //             * Width_i = Top_i - Bottom_i
       //             *
       //             * Then calculate:
       //             *
       //             *                  HighestWidth - LowestWidth
       //             * WidthVariation = --------------------------
       //             *                         AverageWidth
       //             *
       //             * Example:
       //             * HighestWidth = 110
       //             * LowestWidth = 90
       //             * AverageWidth = 100
       //             * WidthVariation = (110 - 90) / 100 = 0.20 = 20%
       //             */
        double widthVariation = GetBandWidthVariation(_lookbackBars);

        /*
         * The width passes when:
         *
         * WidthVariation <= MaximumAllowedWidthVariation
         */
        bool widthIsStable = widthVariation <= _maxWidthVariation;

        /*
         * FINAL FORMULA
         *
         * IsFlat = SlopesAreFlat
         *          AND LinesDoNotBendTooMuch
         *          AND WidthIsStable
         *
         * If even one test is false, IsFlat() returns false.
         */
        return slopesAreFlat && linesDoNotBendTooMuch && widthIsStable;
    }

    /// <summary>
    /// Draws the Top, Main, and Bottom Bollinger lines over the
    /// configured lookback period.
    ///
    /// cTrader draws one straight segment at a time. This method
    /// joins many small segments, so they look like three curved lines.
    /// </summary>
    public void DrawBollingerBands() {
        if (!HasEnoughData())
            return;

        int lastBarIndex = _bars.Count - 1;
        int firstBarIndex = lastBarIndex - _lookbackBars + 1;

        for (int barIndex = firstBarIndex; barIndex < lastBarIndex; barIndex++) {
            int segmentNumber = barIndex - firstBarIndex;

            DrawSegment($"{_chartObjectPrefix}.Top.{segmentNumber}", _bollingerBands.Top, barIndex, Color.Blue);

            DrawSegment($"{_chartObjectPrefix}.Main.{segmentNumber}", _bollingerBands.Main, barIndex, Color.Yellow);

            DrawSegment($"{_chartObjectPrefix}.Bottom.{segmentNumber}", _bollingerBands.Bottom, barIndex, Color.Blue);
        }
    }

    private bool HasEnoughData() {
        /*
         * FORMULA:
         *
         * RequiredBars = LookbackBars
         *                + MAX(BollingerPeriod, AtrPeriod)
         *
         * We need the lookback data plus enough earlier data for
         * both the Bollinger Bands and ATR to become valid.
         */
        int requiredBars = _lookbackBars + Math.Max(_bollingerPeriod, _atrPeriod);

        return _bars.Count >= requiredBars;
    }

    private static bool IsPositiveFiniteNumber(double value) {
        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private void DrawSegment(string name, DataSeries series, int leftBarIndex, Color color) {
        int rightBarIndex = leftBarIndex + 1;

        _chart.DrawTrendLine(name, leftBarIndex, series[leftBarIndex], rightBarIndex, series[rightBarIndex], color, 2, LineStyle.Solid);
    }

    private static double GetLinearRegressionSlope(DataSeries series, int count) {
        /*
         * COMPLETE LINEAR REGRESSION SLOPE FORMULA
         *
         *         SUM((x_i - MeanX) * (y_i - MeanY))
         * Slope = -----------------------------------------
         *                 SUM((x_i - MeanX)^2)
         *
         * x_i:
         *     The time position of each bar.
         *     Oldest bar = 0, next bar = 1, ..., newest = N - 1.
         *
         * y_i:
         *     The Bollinger line value at that bar.
         *
         * MeanX:
         *     The average bar position.
         *
         * MeanY:
         *     The average Bollinger line value.
         *
         * The result is price movement per bar.
         */

        /*
         * Because x is always 0, 1, 2, ..., N - 1:
         *
         * MeanX = (0 + 1 + 2 + ... + (N - 1)) / N
         *       = (N - 1) / 2
         *
         * Example with five bars:
         * MeanX = (0 + 1 + 2 + 3 + 4) / 5 = 2
         */
        double meanX = (count - 1) / 2.0;
        double sumY = 0;

        // Read the values from oldest to newest.
        for (int x = 0; x < count; x++) {
            int offset = count - 1 - x;
            sumY += series.Last(offset);
        }

        /*
         * MeanY = SUM(y_i) / N
         */
        double meanY = sumY / count;
        double numerator = 0;
        double denominator = 0;

        for (int x = 0; x < count; x++) {
            int offset = count - 1 - x;
            double y = series.Last(offset);

            double dx = x - meanX;
            double dy = y - meanY;
            /*
             * Numerator adds:
             *     (x_i - MeanX) * (y_i - MeanY)
             *
             * Denominator adds:
             *     (x_i - MeanX)^2
             */
            numerator += dx * dy;
            denominator += dx * dx;
        }

        if (denominator == 0)
            return 0;
        // Slope = Numerator / Denominator
        return numerator / denominator;
    }

    private static double GetAverage(DataSeries series, int count) {
        /*
         * FORMULA:
         *
         * Average = SUM(all values) / NumberOfValues
         */
        double sum = 0;
        for (int offset = 0; offset < count; offset++) {
            sum += series.Last(offset);
        }

        return sum / count;
    }

    private static double GetRange(DataSeries series, int count) {
        /*
         * FORMULA:
         *
         * Range = MaximumValue - MinimumValue
         */
        double minimum = double.MaxValue;
        double maximum = double.MinValue;

        for (int offset = 0; offset < count; offset++) {
            double value = series.Last(offset);

            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        return maximum - minimum;
    }

    private double GetBandWidthVariation(int count) {
        /*
         * FORMULAS:
         *
         * Width_i = Top_i - Bottom_i
         *
         * AverageWidth = SUM(Width_i) / N
         *
         *                  MaximumWidth - MinimumWidth
         * WidthVariation = ---------------------------
         *                         AverageWidth
         */
        double minimumWidth = double.MaxValue;
        double maximumWidth = double.MinValue;
        double totalWidth = 0;

        for (int offset = 0; offset < count; offset++) {
            double width = _bollingerBands.Top.Last(offset) - _bollingerBands.Bottom.Last(offset);

            minimumWidth = Math.Min(minimumWidth, width);

            maximumWidth = Math.Max(maximumWidth, width);

            totalWidth += width;
        }

        double averageWidth = totalWidth / count;

        if (!IsPositiveFiniteNumber(averageWidth))
            return double.MaxValue;

        return (maximumWidth - minimumWidth) / averageWidth;
    }
}
