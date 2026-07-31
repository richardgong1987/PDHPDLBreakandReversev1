using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Utils {
    // Utils.IsStrategyModeSatisfied 的准入判断：只放行强趋势排列。
    // 多头要求 收盘价 > RMA13(fast) > RMA55(slow)，空头要求 收盘价 < RMA13 < RMA55，两侧都用严格不等号。
    public class StrategyModeTests {
        private const double FastRma = 105.0;
        private const double SlowRma = 100.0;

        [Theory]
        [InlineData(110.0, true)] // 强多头 Close>Fast>Slow
        [InlineData(102.0, false)] // 弱多头 Fast>Close>Slow
        [InlineData(95.0, false)] // 震荡 Fast>Slow>Close
        [InlineData(105.0, false)] // 收盘价贴在快线上，不算突破快线
        public void allows_only_the_strong_bullish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.All, closePrice, FastRma, SlowRma, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(90.0, true)] // 强空头 Close<Fast<Slow
        [InlineData(98.0, false)] // 弱空头 Fast<Close<Slow
        [InlineData(105.0, false)] // 震荡 Fast<Slow<Close
        [InlineData(95.0, false)] // 收盘价贴在快线上，不算跌破快线
        public void allows_only_the_strong_bearish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.All, closePrice, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // 排列自带方向判断，不依赖调用方是否已经做过 RMA 方向过滤：空头排列下问「能不能做多」一律拒绝。
        [Fact]
        public void rejects_a_side_that_contradicts_the_rma_arrangement() {
            Assert.False(IsAllowed(StrategyModel.All, closePrice: 110.0, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(StrategyModel.All, closePrice: 90.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // 快慢线相等时没有排列可言，严格不等号让两侧都被拒绝。
        [Fact]
        public void rejects_both_sides_when_the_two_rma_lines_are_equal() {
            Assert.False(IsAllowed(StrategyModel.All, closePrice: 110.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(StrategyModel.All, closePrice: 90.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        [Theory]
        [InlineData(StrategyModel.All)]
        [InlineData(StrategyModel.MultiplePosition)]
        public void rejects_an_unknown_side(StrategyModel strategy) {
            Assert.False(IsAllowed(strategy, closePrice: 110.0, FastRma, SlowRma, SignalSideModel.None));
        }

        // 这道闸门只看 RMA 排列，与「策略模式」无关：MultiplePosition 的差异在
        // PdhpdlOrderExecutor 的持仓数量闸门里，不在这里。
        [Theory]
        [InlineData(StrategyModel.All)]
        [InlineData(StrategyModel.MultiplePosition)]
        public void gives_the_same_answer_for_every_strategy_mode(StrategyModel strategy) {
            Assert.True(IsAllowed(strategy, closePrice: 110.0, FastRma, SlowRma, SignalSideModel.Buy));
            Assert.False(IsAllowed(strategy, closePrice: 95.0, FastRma, SlowRma, SignalSideModel.Buy));
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
