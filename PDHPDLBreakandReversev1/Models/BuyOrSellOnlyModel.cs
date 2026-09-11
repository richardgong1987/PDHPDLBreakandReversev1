namespace cAlgo.Robots;

// 「策略模式」：按收盘价与双 RMA 的排列强度，隔离出只想交易的那一类行情。
// 由 Utils.IsStrategyModeSatisfied 翻译成 MainBiz 多空两侧的准入判断。
public enum BuyOrSellOnlyModel {
    All, // 全部条件。不作隔离
    BuyOnly, // 持仓情况下，照常能下单
    SellOnly, // 强多头：K线收盘价格>RMA13>RMA55 | 强空头：K线收盘价格<RMA13<RMA55
}
