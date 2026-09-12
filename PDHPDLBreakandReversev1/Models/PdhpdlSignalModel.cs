using System;

namespace cAlgo.Robots;

public class PdhpdlSignalModel {
    public bool HasData { get; set; }

    public DateTime BarTime { get; set; }

    public int BarIndex { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    public double Open { get; set; }

    public double Pdh1 { get; set; }

    public double Pdl1 { get; set; }

    // MarketStructure 到这根 K 线为止最后标出的结构点，及它的编号（第几个）。
    // PivotEntryGate 用前者判断方向、后者判断新旧。
    public MarketStructurePivotModel LatestPivot { get; set; }

    public int PivotCount { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    public double FastRma { get; set; }

    public double SlowRma { get; set; }

    public bool IsLongSignal { get; set; }

    public bool IsShortSignal { get; set; }

    public string Label { get; set; }

    public string KeyLevel { get; set; }

    public double SL { get; set; }

    public StrategyModel Strategy { get; set; }
    public BuyOrSellOnlyModel BuyOrSellOnly { get; set; }

    public bool IsBigK { get; set; }

    // ATR 状态值（H1）= ATR14_H1 ÷ SMA(ATR14_H1,100)。只写进 CSV 供事后分析，不参与进出场判断。
    public double AtrRatioH1 { get; set; } = double.NaN;

    // 昨日区间相对日线波动的宽窄 = (PDH - PDL) ÷ 日线 ATR(14)。同样只用于记录。
    public double PdRangeAtr { get; set; } = double.NaN;

    // H1 的 DMI(14)：趋势强度与多空双方的方向力量。同样只用于记录。
    public double Adx14H1 { get; set; } = double.NaN;

    // 再往前一根已收盘 H1 的 ADX(14)，用来看趋势强度的变化方向。
    public double Adx14H1Previous { get; set; } = double.NaN;

    // 开口扩大 X（GapX）：快慢线开口在回看窗口里扩大了几个 ATR。多头视角，收窄为负。只用于记录。
    // 两个窗口一起记，事后比哪个窗口对胜率更有分辨力（单位是均线来源周期的 K 线根数）。
    public double GapExpansionX3Bar { get; set; } = double.NaN;
    public double GapExpansionX1Bar { get; set; } = double.NaN;

    // 开口扩大闸门的设置，随信号一起传给 MainBiz（见 GapXGate）。
    public bool UseGapX { get; set; }
    public double GapXThreshold { get; set; }
    public double DiPlus14H1 { get; set; } = double.NaN;
    public double DiMinus14H1 { get; set; } = double.NaN;
}
