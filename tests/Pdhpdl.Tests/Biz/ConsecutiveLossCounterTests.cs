using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // 连亏计数器：亏一笔加一、赚一笔清零，连亏够 Nlock 笔才要求走结构点闸门。
    public class ConsecutiveLossCounterTests {
        private const double Loss = -12.5;
        private const double Win = 30.0;

        private static ConsecutiveLossCounter CreateCounter(int nlock = 2) => new(nlock);

        [Fact]
        public void starts_without_requiring_the_pivot_gate() {
            ConsecutiveLossCounter counter = CreateCounter();

            Assert.Equal(0, counter.ConsecutiveLosses);
            Assert.False(counter.IsPivotGateRequired);
        }

        // 差一笔还不算达标：Nlock = 2 时，亏一笔仍然照常放行。
        [Fact]
        public void one_loss_below_nlock_does_not_require_the_pivot_gate() {
            ConsecutiveLossCounter counter = CreateCounter();

            counter.RecordClosedTrade(Loss);

            Assert.Equal(1, counter.ConsecutiveLosses);
            Assert.False(counter.IsPivotGateRequired);
        }

        [Fact]
        public void reaching_nlock_requires_the_pivot_gate() {
            ConsecutiveLossCounter counter = CreateCounter();

            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);

            Assert.Equal(2, counter.ConsecutiveLosses);
            Assert.True(counter.IsPivotGateRequired);
        }

        // 用户描述的那一串：亏、赚（清零）、亏、亏 —— 到第四笔才算连亏两次。
        [Fact]
        public void a_win_in_between_restarts_the_streak() {
            ConsecutiveLossCounter counter = CreateCounter();

            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Win);

            Assert.Equal(0, counter.ConsecutiveLosses);
            Assert.False(counter.IsPivotGateRequired);

            counter.RecordClosedTrade(Loss);
            Assert.False(counter.IsPivotGateRequired);

            counter.RecordClosedTrade(Loss);
            Assert.True(counter.IsPivotGateRequired);
        }

        // 达标之后赢一笔就放松回去，不需要等别的条件。
        [Fact]
        public void a_win_releases_the_pivot_gate_again() {
            ConsecutiveLossCounter counter = CreateCounter();
            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);

            counter.RecordClosedTrade(Win);

            Assert.Equal(0, counter.ConsecutiveLosses);
            Assert.False(counter.IsPivotGateRequired);
        }

        // 不赚不亏按不亏算，和赢一笔一样清零。
        [Fact]
        public void a_breakeven_trade_counts_as_not_a_loss() {
            ConsecutiveLossCounter counter = CreateCounter();
            counter.RecordClosedTrade(Loss);

            counter.RecordClosedTrade(0.0);

            Assert.Equal(0, counter.ConsecutiveLosses);
        }

        // 连亏还在继续累加，闸门一直要求走。
        [Fact]
        public void the_pivot_gate_stays_required_while_the_losses_keep_coming() {
            ConsecutiveLossCounter counter = CreateCounter();
            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);

            counter.RecordClosedTrade(Loss);

            Assert.Equal(3, counter.ConsecutiveLosses);
            Assert.True(counter.IsPivotGateRequired);
        }

        // Nlock <= 0 表示不启用：亏多少笔都不查结构点。
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void nlock_of_zero_or_less_never_requires_the_pivot_gate(int nlock) {
            ConsecutiveLossCounter counter = CreateCounter(nlock);

            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);

            Assert.False(counter.IsEnabled);
            Assert.False(counter.IsPivotGateRequired);
        }

        // Nlock = 1：第一笔亏损就要求走闸门。
        [Fact]
        public void nlock_of_one_requires_the_pivot_gate_after_a_single_loss() {
            ConsecutiveLossCounter counter = CreateCounter(nlock: 1);

            counter.RecordClosedTrade(Loss);

            Assert.True(counter.IsPivotGateRequired);
        }

        // 换线（新的一对 PDH/PDL）就清零：昨天的连亏不带进新的一天，闸门跟着松开。
        [Fact]
        public void reset_clears_the_streak_and_releases_the_pivot_gate() {
            ConsecutiveLossCounter counter = CreateCounter();

            counter.RecordClosedTrade(Loss);
            counter.RecordClosedTrade(Loss);
            Assert.True(counter.IsPivotGateRequired);

            counter.Reset();

            Assert.Equal(0, counter.ConsecutiveLosses);
            Assert.False(counter.IsPivotGateRequired);
        }

        // 清零之后重新数，不留半截历史：Nlock = 2 时还要再亏满两笔才重新要求闸门。
        [Fact]
        public void the_streak_restarts_from_zero_after_a_reset() {
            ConsecutiveLossCounter counter = CreateCounter();

            counter.RecordClosedTrade(Loss);
            counter.Reset();

            counter.RecordClosedTrade(Loss);
            Assert.False(counter.IsPivotGateRequired);

            counter.RecordClosedTrade(Loss);
            Assert.True(counter.IsPivotGateRequired);
        }
    }
}
