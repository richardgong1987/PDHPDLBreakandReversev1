using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Utils {
    // Utils.IsStrategyModeSatisfied gates a side on how the close sits against the two RMA
    // lines. Each StrategyModel branch isolates one arrangement; All/MultiplePosition apply no
    // isolation at all. The arrangement checks carry their own direction test, so the gate does
    // not assume the caller already filtered by RMA direction.
    public class StrategyModeTests {
        // ── All / MultiplePosition: no isolation ──────────────────────────────
        // These two modes differ in the open-position gate inside PdhpdlOrderExecutor, not here,
        // so this gate lets everything through — including a side no arrangement would allow.
        [Theory]
        [InlineData(StrategyModel.All)]
        [InlineData(StrategyModel.MultiplePosition)]
        public void applies_no_isolation(StrategyModel strategy) {
            Assert.True(IsAllowed(strategy, closePrice: 110.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.True(IsAllowed(strategy, closePrice: 95.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.True(IsAllowed(strategy, closePrice: 110.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.None));
        }

        // ── Strong: Close > Fast > Slow (buy) / Close < Fast < Slow (sell) ────
        [Theory]
        [InlineData(110.0, true)]  // strong bull: Close > Fast > Slow
        [InlineData(102.0, false)] // weak bull: Fast > Close > Slow
        [InlineData(95.0, false)]  // chop: Fast > Slow > Close
        [InlineData(105.0, false)] // close sitting on the fast line does not clear it
        public void strong_allows_only_the_strong_bullish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Strong, closePrice, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(90.0, true)]   // strong bear: Close < Fast < Slow
        [InlineData(98.0, false)]  // weak bear: Fast < Close < Slow
        [InlineData(105.0, false)] // chop: Fast < Slow < Close
        [InlineData(95.0, false)]  // close sitting on the fast line does not break it
        public void strong_allows_only_the_strong_bearish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Strong, closePrice, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // A bearish arrangement asked "may I buy?" is rejected, and vice versa.
        [Fact]
        public void strong_rejects_a_side_that_contradicts_the_rma_arrangement() {
            Assert.False(IsAllowed(StrategyModel.Strong, closePrice: 110.0, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(StrategyModel.Strong, closePrice: 90.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // Equal RMA lines are no arrangement at all; the strict inequalities reject both sides.
        [Fact]
        public void strong_rejects_both_sides_when_the_two_rma_lines_are_equal() {
            Assert.False(IsAllowed(StrategyModel.Strong, closePrice: 110.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(StrategyModel.Strong, closePrice: 90.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        [Fact]
        public void strong_rejects_an_unknown_side() {
            Assert.False(IsAllowed(StrategyModel.Strong, closePrice: 110.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.None));
        }

        // ── Weak: the close sits between the two lines ────────────────────────
        [Theory]
        [InlineData(102.0, true)]  // weak bull: Fast(105) > Close > Slow(100)
        [InlineData(110.0, false)] // strong bull: Close is above both lines
        [InlineData(95.0, false)]  // chop: Close is below both lines
        [InlineData(105.0, false)] // close sitting on the fast line is not "between"
        [InlineData(100.0, false)] // close sitting on the slow line is not "between"
        public void weak_allows_only_a_close_between_the_two_lines(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Weak, closePrice, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Buy));
        }

        // Weak reads the arrangement alone — it never looks at the requested side. Both sides,
        // and even None, get the same answer for the same RMA/close geometry.
        [Theory]
        [InlineData(SignalSideModel.Buy)]
        [InlineData(SignalSideModel.Sell)]
        [InlineData(SignalSideModel.None)]
        public void weak_ignores_the_requested_side(SignalSideModel side) {
            Assert.True(IsAllowed(StrategyModel.Weak, closePrice: 102.0, fastRma: 105.0, slowRma: 100.0, side));
            Assert.False(IsAllowed(StrategyModel.Weak, closePrice: 110.0, fastRma: 105.0, slowRma: 100.0, side));
        }

        // ── StopWhenVolatility: Fast > Slow > Close (buy) / Fast < Slow < Close (sell) ──
        // NOTE: this branch returns true for exactly the chop arrangement its enum comment says
        // must not be traded. These tests pin the code as written, not as the comment reads.
        [Theory]
        [InlineData(95.0, true)]   // chop: Fast(105) > Slow(100) > Close
        [InlineData(110.0, false)] // strong bull
        [InlineData(102.0, false)] // weak bull
        [InlineData(100.0, false)] // close sitting on the slow line does not break it
        public void volatility_allows_only_the_chop_arrangement_on_the_buy_side(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.StopWhenVolatility, closePrice, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(105.0, true)]  // chop: Fast(95) < Slow(100) < Close
        [InlineData(90.0, false)]  // strong bear
        [InlineData(98.0, false)]  // weak bear
        [InlineData(100.0, false)] // close sitting on the slow line does not clear it
        public void volatility_allows_only_the_chop_arrangement_on_the_sell_side(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.StopWhenVolatility, closePrice, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        [Fact]
        public void volatility_rejects_an_unknown_side() {
            Assert.False(IsAllowed(StrategyModel.StopWhenVolatility, closePrice: 95.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.None));
        }

        private static bool IsAllowed(StrategyModel strategy, double closePrice, double fastRma, double slowRma, SignalSideModel side) {
            PdhpdlSignalModel signalModel = new() {
                Strategy = strategy,
                HasRmaData = true,
                FastRma = fastRma,
                SlowRma = slowRma
            };
            CandleModel current = new(open: closePrice, high: closePrice, low: closePrice, close: closePrice);

            return cAlgo.Robots.Utils.IsStrategyModeSatisfied(signalModel, current, side);
        }
    }
}
