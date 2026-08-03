namespace cAlgo.Robots;

// The sized order the planner produces from a signal. Pure data, no cAlgo dependency.
public class PdhpdlOrderPlanModel {
    public bool IsValid { get; set; }
    public string RejectReason { get; set; } = "";

    public PdhpdlTradeDirectionModel DirectionModel { get; set; }
    public PdhpdlEntryModel EntryModel { get; set; }
    public bool IsMarketOrder { get; set; }

    public double EntryPrice { get; set; }
    public double StopPrice { get; set; }
    public double TakeProfitPrice { get; set; }
    public double RiskPrice { get; set; }
    public double StopLossPips { get; set; }
    public double TakeProfitPips { get; set; }

    public double Lots { get; set; }
    public double VolumeInUnits { get; set; }

    public double AccountEquity { get; set; }
    public double RiskMoney { get; set; }
    public double EstimatedRiskMoney { get; set; }
    public string Label { get; set; } = "";

    public string KeyLevel { get; set; } = "";
    public string SignalName { get; set; } = "";

    // 产生该计划的那根收盘 K 线。挂单用它计时：过了 N 根 K 线还没成交就撤单。
    public int SignalBarIndex { get; set; }
}
