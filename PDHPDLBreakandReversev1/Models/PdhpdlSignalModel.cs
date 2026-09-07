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
    public double Pdh2 { get; set; }
    public double Pdh3 { get; set; }
    public double Pdh4 { get; set; }

    public double Pdl1 { get; set; }
    public double Pdl2 { get; set; }
    public double Pdl3 { get; set; }
    public double Pdl4 { get; set; }

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

    // 这根信号 K 线上的 ATR14。下单成功时会被记下来，
    // 作为这笔单万一亏损、触发连亏锁仓时的解锁参照（见 ConsecutiveLossLock）。
    public double Atr { get; set; }
    public bool IsBigK { get; set; }
}
