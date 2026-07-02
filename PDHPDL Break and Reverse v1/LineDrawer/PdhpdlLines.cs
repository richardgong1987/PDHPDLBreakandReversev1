using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlLines {
    private const string Prefix = "PDH_PDL_STEP_";

    private readonly Chart _chart;
    private readonly Bars _dailyBars;
    private readonly List<string> _objectNames = new();

    private readonly int _daysToDraw;
    private readonly int _thickness;

    private DateTime _lastDailyOpenTime = DateTime.MinValue;

    public PdhpdlLines(Chart chart, MarketData marketData, string symbolName, int daysToDraw, int thickness) {
        _chart = chart;
        _dailyBars = marketData.GetBars(TimeFrame.Daily, symbolName);
        _daysToDraw = daysToDraw;
        _thickness = thickness;
    }

    public void Draw() {
        if (_dailyBars.Count < 2)
            return;

        DateTime currentDailyOpenTime = _dailyBars.OpenTimes[_dailyBars.Count - 1];

        if (currentDailyOpenTime == _lastDailyOpenTime)
            return;

        _lastDailyOpenTime = currentDailyOpenTime;

        Clear();

        int startIndex = Math.Max(1, _dailyBars.Count - _daysToDraw);

        for (int i = startIndex; i < _dailyBars.Count; i++) {
            DateTime startTime = _dailyBars.OpenTimes[i];

            DateTime endTime = i + 1 < _dailyBars.Count ? _dailyBars.OpenTimes[i + 1] : startTime.AddDays(1);

            double pdh = _dailyBars.HighPrices[i - 1];
            double pdl = _dailyBars.LowPrices[i - 1];

            string dateKey = startTime.ToString("yyyyMMdd");

            DrawSegment($"{Prefix}PDH_{dateKey}", startTime, endTime, pdh, Color.Red);

            DrawSegment($"{Prefix}PDL_{dateKey}", startTime, endTime, pdl, Color.Lime);

            if (i > startIndex && i > 1) {
                double previousPdh = _dailyBars.HighPrices[i - 2];
                double previousPdl = _dailyBars.LowPrices[i - 2];

                DrawVerticalConnector($"{Prefix}PDH_CONNECTOR_{dateKey}", startTime, previousPdh, pdh, Color.Red);

                DrawVerticalConnector($"{Prefix}PDL_CONNECTOR_{dateKey}", startTime, previousPdl, pdl, Color.Lime);
            }
        }
    }

    private void DrawSegment(string name, DateTime startTime, DateTime endTime, double price, Color color) {
        _chart.DrawTrendLine(name, startTime, price, endTime, price, color, _thickness, LineStyle.Solid);

        _objectNames.Add(name);
    }

    private void DrawVerticalConnector(string name, DateTime time, double fromPrice, double toPrice, Color color) {
        _chart.DrawTrendLine(name, time, fromPrice, time, toPrice, color, _thickness, LineStyle.Solid);

        _objectNames.Add(name);
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }
}
