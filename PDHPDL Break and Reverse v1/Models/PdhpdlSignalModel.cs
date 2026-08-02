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

    public double Pdh { get; set; }

    public double Pdl { get; set; }

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
    public int Nlock { get; set; }
    public int LossCount { get; set; }
    // 这根信号 K 线上的 ATR14，以及「振幅算大」的倍数门槛。
    // 下单成功时 Atr 会被记下来，作为这笔单万一亏损后的锁仓参照。
    public double Atr { get; set; }

    public double MaxBarRangeAtr { get; set; }
    public bool IsBigK { get; set; }
}
