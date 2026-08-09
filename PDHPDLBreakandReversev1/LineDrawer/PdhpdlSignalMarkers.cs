using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalMarkers {
    private const string Prefix = "PDH_PDL_SIGNAL_";

    private const int IconOffsetTicks = 120;
    private const int TextOffsetTicks = 320;
    private const int TextFontSize = 8;

    private readonly Chart _chart;
    private readonly double _iconOffset;
    private readonly double _textOffset;
    private readonly HashSet<string> _objectNames = new();

    public PdhpdlSignalMarkers(Chart chart, double tickSize) {
        _chart = chart;
        _iconOffset = tickSize * IconOffsetTicks;
        _textOffset = tickSize * TextOffsetTicks;
    }

    public void Draw(PdhpdlSignalModel signalModel) {
        if (signalModel == null || !signalModel.HasData)
            return;

        if (signalModel.IsLongSignal)
            DrawLong(signalModel);

        if (signalModel.IsShortSignal)
            DrawShort(signalModel);
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawLong(PdhpdlSignalModel signalModel) {
        string key = GetKey(signalModel);

        double iconPrice = signalModel.Low - _iconOffset;
        double textPrice = signalModel.Low - _textOffset;

        string iconName = $"{Prefix}LONG_ICON_{key}";
        string textName = $"{Prefix}LONG_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.UpTriangle, signalModel.BarIndex, iconPrice, Color.Lime);

        ChartText text = _chart.DrawText(textName, signalModel.Label, signalModel.BarIndex, textPrice, Color.Lime);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private void DrawShort(PdhpdlSignalModel signalModel) {
        string key = GetKey(signalModel);

        double iconPrice = signalModel.High + _iconOffset;
        double textPrice = signalModel.High + _textOffset;

        string iconName = $"{Prefix}SHORT_ICON_{key}";
        string textName = $"{Prefix}SHORT_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.DownTriangle, signalModel.BarIndex, iconPrice, Color.Red);

        ChartText text = _chart.DrawText(textName, signalModel.Label, signalModel.BarIndex, textPrice, Color.Red);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private static void ApplyTextStyle(ChartText text) {
        text.FontSize = TextFontSize;
        text.IsBold = false;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }

    private void RemoveExisting(string name) {
        _chart.RemoveObject(name);
        _objectNames.Remove(name);
    }

    private static string GetKey(PdhpdlSignalModel signalModel) {
        return signalModel.BarTime.ToString("yyyyMMdd_HHmmss");
    }
}
