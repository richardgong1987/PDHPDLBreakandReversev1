namespace cAlgo.Robots;

// MarketStructure 最近一次新确认的结构点，对应图上最后画出来的那个标签。
// HigherHigh / HigherLow 是绿色标记（多头结构），LowerLow / LowerHigh 是红色标记（空头结构）。
public enum MarketStructurePivotModel {
    None,
    HigherHigh,
    HigherLow,
    LowerHigh,
    LowerLow
}
