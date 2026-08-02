namespace cAlgo.Robots;

// 当前连续亏损了多少笔，以及最近那笔亏损单「开仓时」的 ATR。
// 全局计数：所有平仓单一起算，不区分关键位、方向、入场模式。
// 亏损的口径与交易 CSV 的「盈利/亏损」一致：净盈亏为负才算亏，持平按盈利处理、连亏归零。
//
// 记住开仓时的 ATR，是为了给「连亏锁仓」当参照：解锁要求后面某根 K 线的振幅超过它的若干倍，
// 也就是「行情比我上次入场时活跃得多」。用当时的 ATR 而不是当前 ATR——行情冷下来时当前
// ATR 会一起降，门槛跟着降，等于自动解锁，那就失去锁仓的意义了。
public class ConsecutiveLossCounter {
    public int Count { get; private set; }

    public double AtrAtLastLossEntry { get; private set; }

    public void RecordClosedTrade(double netProfit, double atrAtEntry) {
        if (netProfit >= 0.0) {
            Count = 0;
            AtrAtLastLossEntry = 0.0;
            return;
        }

        Count++;
        AtrAtLastLossEntry = atrAtEntry;
    }
}
