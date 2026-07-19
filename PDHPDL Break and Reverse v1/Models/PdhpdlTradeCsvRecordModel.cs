using System;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvRecordModel {
    public string Id { get; set; }

    public string KeyLevel { get; set; }

    public string Signal { get; set; }

    public string EntryMode { get; set; }

    public string Comment { get; set; }

    public string Symbol { get; set; }

    public string TimeFrame { get; set; }

    public double EntryAccountEquity { get; set; }

    public double CloseAccountEquity { get; set; }

    public DateTime EntryTime { get; set; }

    public double EntryPrice { get; set; }

    public double ClosePrice { get; set; }

    public double StopPrice { get; set; }

    public double TakeProfitPrice { get; set; }

    public double RiskPrice { get; set; }

    public double VolumeInUnits { get; set; }

    public string CloseReason { get; set; }

    public double ProfitLoss { get; set; }

    public string CloseTime { get; set; }

    public string PendingOrderId { get; set; }

    public string PositionId { get; set; }

    public string DealId { get; set; }
}
