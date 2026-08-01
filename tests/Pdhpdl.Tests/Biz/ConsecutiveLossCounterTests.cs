using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    public class ConsecutiveLossCounterTests {
        [Fact]
        public void starts_at_zero_before_any_trade_closes() {
            ConsecutiveLossCounter counter = new();

            Assert.Equal(0, counter.Count);
        }

        [Fact]
        public void counts_each_losing_trade_in_a_row() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(-3.5);
            counter.RecordClosedTrade(-0.01);

            Assert.Equal(3, counter.Count);
        }

        [Fact]
        public void resets_when_a_trade_wins() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(20.0);

            Assert.Equal(0, counter.Count);
        }

        // 与交易 CSV 的「盈利/亏损」口径一致：持平记为盈利，连亏归零。
        [Fact]
        public void resets_when_a_trade_breaks_even() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(0.0);

            Assert.Equal(0, counter.Count);
        }

        [Fact]
        public void starts_the_streak_over_after_a_reset() {
            ConsecutiveLossCounter counter = new();

            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(-10.0);
            counter.RecordClosedTrade(20.0);
            counter.RecordClosedTrade(-10.0);

            Assert.Equal(1, counter.Count);
        }
    }
}
