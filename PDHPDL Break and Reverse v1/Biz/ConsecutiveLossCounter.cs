namespace cAlgo.Robots;

// 当前连续亏损了多少笔。全局计数：所有平仓单一起算，不区分关键位、方向、入场模式。
// 只统计、不拦截——任何下单决策都不读它，它的唯一出口是平仓时的日志。
// 亏损的口径与交易 CSV 的「盈利/亏损」一致：净盈亏为负才算亏，持平按盈利处理、连亏归零。
public class ConsecutiveLossCounter {
    public int Count { get; private set; }

    public void RecordClosedTrade(double netProfit) {
        Count = netProfit < 0.0 ? Count + 1 : 0;
    }
}
