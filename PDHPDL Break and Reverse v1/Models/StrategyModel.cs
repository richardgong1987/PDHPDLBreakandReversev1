namespace cAlgo.Robots;

// 「策略模式」：选择生产策略，或只回测单个信号（可再指定关键位 PDH/PDL）的单个方向。
// 由 StrategyModePolicy 翻译成 MainBiz 需要的四个开关：方向 / 信号家族 / 突破分支 / 关键位。
public enum StrategyModel {
    All, // 全部条件。不作隔离
    Strong, // 强多头：K线收盘价格>RMA13>RMA55 | 强空头：K线收盘价格<RMA13<RMA55
    Weak, // 弱多头：RMA13>K线收盘价格>RMA55   | 弱空头：RMA13<K线收盘价格<RMA55
    StopWhenVolatility // 趋势转换或者震荡：RMA13>RMA55>K线收盘价格  （不交易） | 趋势转换或者震荡：RMA13<RMA55<K线收盘价格 （不交易）
}
