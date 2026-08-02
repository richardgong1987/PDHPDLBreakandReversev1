using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    public class ConsecutiveLossCounterTests {
        private const double AnyAtr = 8.0;

        [Fact]
        public void starts_at_zero_before_any_trade_closes() {
            ConsecutiveLossCounter counter = new();

            Assert.Equal(0, counter.Count);
            Assert.Equal(0.0, counter.AtrAtLastLossEntry);
        }

        [Fact]
        public void counts_each_losing_trade_in_a_row() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(-3.5, AnyAtr);
            counter.RecordClosedTrade(-0.01, AnyAtr);

            Assert.Equal(3, counter.Count);
        }

        [Fact]
        public void resets_when_a_trade_wins() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(20.0, AnyAtr);

            Assert.Equal(0, counter.Count);
        }

        // 与交易 CSV 的「盈利/亏损」口径一致：持平记为盈利，连亏归零。
        [Fact]
        public void resets_when_a_trade_breaks_even() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(0.0, AnyAtr);

            Assert.Equal(0, counter.Count);
        }

        [Fact]
        public void starts_the_streak_over_after_a_reset() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(-10.0, AnyAtr);
            counter.RecordClosedTrade(20.0, AnyAtr);
            counter.RecordClosedTrade(-10.0, AnyAtr);

            Assert.Equal(1, counter.Count);
        }

        [Fact]
        public void remembers_the_entry_atr_of_the_latest_losing_trade() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, 8.0);
            counter.RecordClosedTrade(-10.0, 5.0);

            Assert.Equal(5.0, counter.AtrAtLastLossEntry);
        }

        // 赢一单清空连亏，参照 ATR 一并作废——下次锁仓要用新的亏损单重新记。
        [Fact]
        public void forgets_the_entry_atr_when_a_trade_wins() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, 8.0);
            counter.RecordClosedTrade(20.0, 3.0);

            Assert.Equal(0.0, counter.AtrAtLastLossEntry);
        }

        // 盈利单的 ATR 不该覆盖参照：只有亏损单才更新它。
        [Fact]
        public void keeps_the_losing_entry_atr_across_a_later_loss_only() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0, 8.0);
            counter.RecordClosedTrade(-10.0, 9.0);

            Assert.Equal(2, counter.Count);
            Assert.Equal(9.0, counter.AtrAtLastLossEntry);
        }
    }
}
