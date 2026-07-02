using System;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvRecord {
    public string Id { get; set; }

    public string Side { get; set; }

    public string KeyLevel { get; set; }

    public string Signal { get; set; }

    public string CloseEntryResult { get; set; }

    public string Pullback25Result { get; set; }

    public string Pullback382Result { get; set; }

    public string Pullback50Result { get; set; }

    public string Comment { get; set; }

    public string Symbol { get; set; }

    public string TimeFrame { get; set; }

    public DateTime EntryTime { get; set; }

    public double EntryPrice { get; set; }

    public double StopPrice { get; set; }

    public double Tp1Price { get; set; }

    public double Tp2Price { get; set; }

    public double RiskPrice { get; set; }

    public double VolumeInUnits { get; set; }

    public string CloseReason { get; set; }

    public double ProfitLoss { get; set; }

    public string CloseTime { get; set; }

    public string CtraderId { get; set; }
}
