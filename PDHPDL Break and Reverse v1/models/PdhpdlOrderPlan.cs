using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlOrderPlan {
    public bool IsValid { get; set; }

    public string RejectReason { get; set; }

    public TradeType TradeType { get; set; }

    public PdhpdlEntryMode EntryMode { get; set; }

    public bool IsMarketOrder { get; set; }

    public double EntryPrice { get; set; }

    public double StopPrice { get; set; }

    public double Tp1Price { get; set; }

    public double Tp2Price { get; set; }

    public double RiskPrice { get; set; }

    public double StopLossPips { get; set; }

    public double Tp1Pips { get; set; }

    public double Tp2Pips { get; set; }

    public double TotalLots { get; set; }

    public double TotalVolumeInUnits { get; set; }

    public double VolumePerLegInUnits { get; set; }

    public double RiskMoney { get; set; }

    public double EstimatedRiskMoney { get; set; }

    public string Tp1Label { get; set; }

    public string RunnerLabel { get; set; }
}
