using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// C# port of the "MA 1 + MA 2" Pine indicator: two Welles-Wilder (ta.rma)
// moving averages overlaid on the chart.
//
// Two selectable sources (see MovingAverageSourceModel):
//   HigherTimeFrame - RMA on 120-minute bars, drawn as a stepped line that
//                     holds each value until the higher-timeframe bar confirms.
//                     Faithful to request.security(tickerid, "120", ...).
//   ChartTimeFrame  - RMA on the chart's own bars, one point per candle. Tracks
//                     price without the higher-timeframe stepping lag.
//
// MA 1 (fast) is blue, MA 2 (slow) is orange-red, matching the Pine colours.
public class DualRmaLines {
    private const string Prefix = "DUAL_RMA_";

    // Cap the number of segments per line so a long chart cannot spawn an
    // unbounded number of chart objects.
    private const int MaxSegmentsPerLine = 500;

    private static readonly Color FastColor = Color.Blue;
    private static readonly Color SlowColor = Color.OrangeRed;

    private readonly Chart _chart;
    private readonly Bars _chartBars;
    private readonly Bars _sourceBars;
    private readonly IndicatorDataSeries _fastValues;
    private readonly IndicatorDataSeries _slowValues;
    private readonly MovingAverageSourceModel _source;
    private readonly int _thickness;
    private readonly List<string> _objectNames = new();

    private DateTime _lastChartOpenTime = DateTime.MinValue;

    public DualRmaLines(Chart chart, Bars chartBars, DualRmaSeries rmaSeries, int thickness) {
        _chart = chart;
        _chartBars = chartBars;
        _source = rmaSeries.Source;
        _sourceBars = rmaSeries.SourceBars;
        _fastValues = rmaSeries.FastValues;
        _slowValues = rmaSeries.SlowValues;
        _thickness = thickness;
    }

    // Redraw once per chart bar so the higher-timeframe line's held tail keeps
    // reaching the latest candle instead of trailing behind it.
    public void Draw() {
        if (_sourceBars.Count < 2 || _chartBars.Count < 1)
            return;

        DateTime currentChartOpenTime = _chartBars.OpenTimes[_chartBars.Count - 1];

        if (currentChartOpenTime == _lastChartOpenTime)
            return;

        _lastChartOpenTime = currentChartOpenTime;

        Clear();

        int startIndex = Math.Max(1, _sourceBars.Count - MaxSegmentsPerLine);

        if (_source == MovingAverageSourceModel.ChartTimeFrame) {
            DrawSmoothLine("FAST", startIndex, _fastValues, FastColor);
            DrawSmoothLine("SLOW", startIndex, _slowValues, SlowColor);
        } else {
            DrawSteppedLine("FAST", startIndex, _fastValues, FastColor);
            DrawSteppedLine("SLOW", startIndex, _slowValues, SlowColor);
        }
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    // Chart-timeframe mode: join consecutive per-bar averages into one line.
    private void DrawSmoothLine(string lineKey, int startIndex, IndicatorDataSeries values, Color color) {
        for (int i = startIndex; i < _sourceBars.Count - 1; i++) {
            double fromValue = values[i];
            double toValue = values[i + 1];

            if (double.IsNaN(fromValue) || double.IsNaN(toValue))
                continue;

            DrawSegment($"{Prefix}{lineKey}_{_sourceBars.OpenTimes[i]:yyyyMMddHHmm}",
                _sourceBars.OpenTimes[i], fromValue, _sourceBars.OpenTimes[i + 1], toValue, color);
        }
    }

    // Higher-timeframe mode: replicate request.security with lookahead off. A
    // higher-timeframe average value is only known once its bar closes, so it is
    // held flat from that close until the next close, then steps to the new
    // value. The last confirmed value is held out to the current chart time.
    private void DrawSteppedLine(string lineKey, int startIndex, IndicatorDataSeries values, Color color) {
        DateTime currentTime = _chartBars.OpenTimes[_chartBars.Count - 1];
        int lastConfirmedIndex = _sourceBars.Count - 2;

        for (int i = startIndex; i <= lastConfirmedIndex; i++) {
            double value = values[i];

            if (double.IsNaN(value))
                continue;

            DateTime holdStart = _sourceBars.OpenTimes[i + 1];
            DateTime holdEnd = i + 2 < _sourceBars.Count ? _sourceBars.OpenTimes[i + 2] : currentTime;

            if (holdEnd <= holdStart)
                holdEnd = currentTime;

            DrawSegment($"{Prefix}{lineKey}_H_{holdStart:yyyyMMddHHmm}", holdStart, value, holdEnd, value, color);

            DrawStepConnector(lineKey, startIndex, i, holdStart, value, values, color);
        }
    }

    // Vertical jump between the previous held value and the current one.
    private void DrawStepConnector(string lineKey, int startIndex, int index, DateTime stepTime, double value,
        IndicatorDataSeries values, Color color) {
        if (index - 1 < startIndex)
            return;

        double previousValue = values[index - 1];

        if (double.IsNaN(previousValue))
            return;

        DrawSegment($"{Prefix}{lineKey}_V_{stepTime:yyyyMMddHHmm}", stepTime, previousValue, stepTime, value, color);
    }

    private void DrawSegment(string name, DateTime fromTime, double fromValue, DateTime toTime, double toValue, Color color) {
        _chart.DrawTrendLine(name, fromTime, fromValue, toTime, toValue, color, _thickness, LineStyle.Solid);

        _objectNames.Add(name);
    }

}
