namespace cAlgo.Robots;

// The sized order the planner produces from a signal. Pure data, no cAlgo dependency.
public class PdhpdlOrderPlan {
    public bool IsValid { get; set; }
    public string RejectReason { get; set; } = "";

    public PdhpdlTradeDirection Direction { get; set; }
    public PdhpdlEntryMode EntryMode { get; set; }
    public bool IsMarketOrder { get; set; }

    public double EntryPrice { get; set; }
    public double StopPrice { get; set; }
    public double TakeProfitPrice { get; set; }
    public double RiskPrice { get; set; }
    public double StopLossPips { get; set; }
    public double TakeProfitPips { get; set; }

    public double TotalLots { get; set; }
    public double TotalVolumeInUnits { get; set; }
    public double NativeRiskVolumeInUnits { get; set; }
    public double PriceRiskCappedVolumeInUnits { get; set; }

    public double AccountEquity { get; set; }
    public double RiskMoney { get; set; }
    public double EstimatedRiskMoney { get; set; }

    public string Label { get; set; } = "";
}
