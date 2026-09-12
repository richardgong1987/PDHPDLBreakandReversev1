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

    // ATR14_H1 / SMA(ATR14_H1,100)
    public double AtrRatioH1 { get; set; } = double.NaN;

    // (PDH - PDL) / 日线 ATR(14)
    public double PdRangeAtr { get; set; } = double.NaN;

    // H1 DMI(14)
    public double Adx14H1 { get; set; } = double.NaN;
    public double Adx14H1Previous { get; set; } = double.NaN;
    public double DiPlus14H1 { get; set; } = double.NaN;
    public double DiMinus14H1 { get; set; } = double.NaN;

    // 开口扩大 X：(现在的快慢线开口 - 回看 N 根之前的开口) / 均线周期 ATR(14)。N = 3 根 / 1 根两种窗口。
    public double GapExpansionX3Bar { get; set; } = double.NaN;
    public double GapExpansionX1Bar { get; set; } = double.NaN;
}
