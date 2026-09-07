namespace cAlgo.Robots;

// 「策略模式」：按收盘价与双 RMA 的排列强度，隔离出只想交易的那一类行情。
// 由 Utils.IsStrategyModeSatisfied 翻译成 MainBiz 多空两侧的准入判断。
public enum StrategyModel {
    All, // 全部条件。不作隔离
    MultiplePosition,// 持仓情况下，照常能下单
    Strong, // 强多头：K线收盘价格>RMA13>RMA55 | 强空头：K线收盘价格<RMA13<RMA55
    Weak, // 弱多头：RMA13>K线收盘价格>RMA55   | 弱空头：RMA13<K线收盘价格<RMA55
    StrongWeak,
    StopWhenVolatility // 趋势转换或者震荡：RMA13>RMA55>K线收盘价格  （不交易） | 趋势转换或者震荡：RMA13<RMA55<K线收盘价格 （不交易）
}
