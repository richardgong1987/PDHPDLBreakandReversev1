using System;

namespace cAlgo.Robots;

public class PdhpdlSignal {
    public bool HasData { get; set; }

    public DateTime BarTime { get; set; }

    public int BarIndex { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    public double Pdh { get; set; }

    public double Pdl { get; set; }

    public bool IsLongSignal { get; set; }

    public bool IsShortSignal { get; set; }
}
