using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalMarkers {
    private const string Prefix = "PDH_PDL_SIGNAL_";

    private const int IconOffsetTicks = 120;
    private const int TextOffsetTicks = 360;
    private const int TextFontSize = 18;

    private readonly Chart _chart;
    private readonly double _iconOffset;
    private readonly double _textOffset;
    private readonly HashSet<string> _objectNames = new();

    public PdhpdlSignalMarkers(Chart chart, double tickSize) {
        _chart = chart;
        _iconOffset = tickSize * IconOffsetTicks;
        _textOffset = tickSize * TextOffsetTicks;
    }

    public void Draw(PdhpdlSignal signal) {
        if (signal == null || !signal.HasData)
            return;

        if (signal.IsLongSignal)
            DrawLong(signal);

        if (signal.IsShortSignal)
            DrawShort(signal);
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawLong(PdhpdlSignal signal) {
        string key = GetKey(signal);

        double iconPrice = signal.Low - _iconOffset;
        double textPrice = signal.Low - _textOffset;

        string iconName = $"{Prefix}LONG_ICON_{key}";
        string textName = $"{Prefix}LONG_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.UpTriangle, signal.BarIndex, iconPrice, Color.Lime);

        ChartText text = _chart.DrawText(textName, "L", signal.BarIndex, textPrice, Color.Lime);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private void DrawShort(PdhpdlSignal signal) {
        string key = GetKey(signal);

        double iconPrice = signal.High + _iconOffset;
        double textPrice = signal.High + _textOffset;

        string iconName = $"{Prefix}SHORT_ICON_{key}";
        string textName = $"{Prefix}SHORT_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.DownTriangle, signal.BarIndex, iconPrice, Color.Red);

        ChartText text = _chart.DrawText(textName, "S", signal.BarIndex, textPrice, Color.Red);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private static void ApplyTextStyle(ChartText text) {
        text.FontSize = TextFontSize;
        text.IsBold = true;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }

    private void RemoveExisting(string name) {
        _chart.RemoveObject(name);
        _objectNames.Remove(name);
    }

    private static string GetKey(PdhpdlSignal signal) {
        return signal.BarTime.ToString("yyyyMMdd_HHmmss");
    }
}
