using cAlgo.API.Internals;

namespace cAlgo.Robots;

// 开口扩大 X：(现在的快慢线开口 - N 根之前的开口) / ATR14，单位是 ATR 倍数。
// 快慢线与 ATR 一律取趋势均线所在的那个 HTF 周期（DualRmaSeries.SourceBars）——
// 用图表周期的 ATR 去除高周期均线的间距，分子分母量纲不同，算出来的倍数没有意义。
// 公式搬自 MovingAverageV1 的开口扩大闸门。
public class GapXSeries {
    // 闸门用的回看窗口，单位是均线周期的 K 线根数。3 根是 MovingAverageV1 参数根数的直接移植
    //（它的参数按 15 分钟 K 线计数，默认 12 根 = 180 分钟，换算到它 60 分钟的均线周期正好 3 根）。
    public const int GateLookbackBars = 3;

    // 只写进 CSV、用来和上面那个窗口比分辨力的短窗口，不参与闸门判断。
    public const int ShortLookbackBars = 1;

    private readonly DualRmaSeries _rmaSeries;
    private readonly Atr14Series _sourceAtr14;
    private readonly PdhpdlGapXConfigModel _config;

    public GapXSeries(IIndicatorsAccessor indicators, DualRmaSeries rmaSeries, PdhpdlGapXConfigModel config) {
        _rmaSeries = rmaSeries;
        _sourceAtr14 = new Atr14Series(indicators, rmaSeries.SourceBars);
        _config = config;
    }

    // 把两个窗口的 GapX 和闸门设置一起放到信号上：MainBiz 只读信号，不认识指标序列。
    // 必须在 MainBiz.Evaluate 之前调用。
    public void Fill(PdhpdlSignalModel signalModel) {
        signalModel.GapExpansionX3Bar = Calculate(GateLookbackBars);
        signalModel.GapExpansionX1Bar = Calculate(ShortLookbackBars);
        signalModel.UseGapX = _config.IsEnabled;
        signalModel.GapXThreshold = _config.Threshold;
    }

    // 数据不足（均线未确认、ATR 暖机、历史不够 lookbackBars 根）时返回 NaN，CSV 里写成空。
    public double Calculate(int lookbackBars) {
        if (!_sourceAtr14.TryGetValue(_rmaSeries.ConfirmedIndex, out double atr))
            return double.NaN;

        if (!_rmaSeries.TryGetGap(0, out double currentGap))
            return double.NaN;

        if (!_rmaSeries.TryGetGap(lookbackBars, out double pastGap))
            return double.NaN;

        return (currentGap - pastGap) / atr;
    }
}
