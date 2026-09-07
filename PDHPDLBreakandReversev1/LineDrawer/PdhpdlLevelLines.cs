using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// 手工指定的 PDH/PDL 价位（Pdh1/Pdl1），横贯整个图表的水平线。
// 0 表示该档没有设置，不画。
public class PdhpdlLevelLines {
    private const string Prefix = "PDH_PDL_LEVEL_";

    private static readonly Color PdhColor = Color.Green;
    private static readonly Color PdlColor = Color.Red;

    private readonly Chart _chart;
    private readonly int _thickness;
    private readonly List<string> _objectNames = new();

    public PdhpdlLevelLines(Chart chart, int thickness) {
        _chart = chart;
        _thickness = thickness;
    }

    public void Draw(ParameterModel parameter) {
        Clear();
        DrawLevel("PDH1", parameter.Pdh1, PdhColor);
        DrawLevel("PDL1", parameter.Pdl1, PdlColor);
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawLevel(string levelKey, double price, Color color) {
        if (price <= 0)
            return;

        string name = Prefix + levelKey;

        _chart.DrawHorizontalLine(name, price, color, _thickness, LineStyle.Solid);

        _objectNames.Add(name);
    }
}
