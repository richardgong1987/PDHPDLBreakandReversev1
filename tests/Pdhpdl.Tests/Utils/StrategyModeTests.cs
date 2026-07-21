using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Utils {
    // 「策略模式」的准入判断：收盘价相对 RMA13(fast) / RMA55(slow) 的排列强度。
    public class StrategyModeTests {
        private const double FastRma = 105.0;
        private const double SlowRma = 100.0;

        [Theory]
        [InlineData(110.0, SignalSideModel.Buy)] // 强多头 Close>Fast>Slow
        [InlineData(102.0, SignalSideModel.Buy)] // 弱多头 Fast>Close>Slow
        [InlineData(95.0, SignalSideModel.Buy)] // 震荡 Fast>Slow>Close
        [InlineData(110.0, SignalSideModel.Sell)]
        [InlineData(95.0, SignalSideModel.Sell)]
        public void all_mode_allows_every_arrangement(double closePrice, SignalSideModel side) {
            Assert.True(IsAllowed(StrategyModel.All, closePrice, FastRma, SlowRma, side));
        }

        [Theory]
        [InlineData(110.0, true)] // 强多头 Close>Fast>Slow
        [InlineData(102.0, false)] // 弱多头
        [InlineData(95.0, false)] // 震荡
        [InlineData(105.0, false)] // 收盘价贴在快线上，不算突破快线
        public void strong_mode_allows_only_strong_bullish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Strong, closePrice, FastRma, SlowRma, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(90.0, true)] // 强空头 Close<Fast<Slow
        [InlineData(98.0, false)] // 弱空头
        [InlineData(105.0, false)] // 震荡
        public void strong_mode_allows_only_strong_bearish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Strong, closePrice, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        [Theory]
        [InlineData(110.0, false)] // 强多头
        [InlineData(102.0, true)] // 弱多头 Fast>Close>Slow
        [InlineData(95.0, false)] // 震荡
        [InlineData(100.0, false)] // 收盘价贴在慢线上，已跌出弱多头区间
        public void weak_mode_allows_only_weak_bullish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Weak, closePrice, FastRma, SlowRma, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(90.0, false)] // 强空头
        [InlineData(98.0, true)] // 弱空头 Fast<Close<Slow
        [InlineData(105.0, false)] // 震荡
        public void weak_mode_allows_only_weak_bearish_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.Weak, closePrice, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        [Theory]
        [InlineData(110.0, true)] // 强多头
        [InlineData(102.0, true)] // 弱多头
        [InlineData(95.0, false)] // 震荡 Fast>Slow>Close，不交易
        public void stop_when_volatility_mode_blocks_only_the_bullish_volatility_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.StopWhenVolatility, closePrice, FastRma, SlowRma, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(90.0, true)] // 强空头
        [InlineData(98.0, true)] // 弱空头
        [InlineData(105.0, false)] // 震荡 Fast<Slow<Close，不交易
        public void stop_when_volatility_mode_blocks_only_the_bearish_volatility_arrangement(double closePrice, bool expected) {
            Assert.Equal(expected, IsAllowed(StrategyModel.StopWhenVolatility, closePrice, fastRma: 95.0, slowRma: 100.0,
                SignalSideModel.Sell));
        }

        // Strong / Weak 自带排列的方向判断，不依赖调用方是否已经做过 RMA 方向过滤：
        // 空头排列下问「能不能做多」一律拒绝。
        [Theory]
        [InlineData(StrategyModel.Strong)]
        [InlineData(StrategyModel.Weak)]
        public void strong_and_weak_reject_a_side_that_contradicts_the_rma_arrangement(StrategyModel strategy) {
            Assert.False(IsAllowed(strategy, closePrice: 110.0, fastRma: 95.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(strategy, closePrice: 90.0, fastRma: 105.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // 快慢线相等时没有排列可言，Strong / Weak 要求严格不等号，两侧都拒绝。
        [Theory]
        [InlineData(StrategyModel.Strong)]
        [InlineData(StrategyModel.Weak)]
        public void strong_and_weak_reject_both_sides_when_the_two_rma_lines_are_equal(StrategyModel strategy) {
            Assert.False(IsAllowed(strategy, closePrice: 110.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Buy));
            Assert.False(IsAllowed(strategy, closePrice: 90.0, fastRma: 100.0, slowRma: 100.0, SignalSideModel.Sell));
        }

        // StopWhenVolatility 是黑名单，只排除震荡排列本身；方向是否与排列相符、
        // 以及快慢线相等这两种情况，都交给 MainBiz 里的 RMA 方向闸门处理。
        [Fact]
        public void stop_when_volatility_only_blocks_the_volatility_arrangement_of_the_asked_side() {
            Assert.True(IsAllowed(StrategyModel.StopWhenVolatility, closePrice: 110.0, fastRma: 95.0, slowRma: 100.0,
                SignalSideModel.Buy));
            Assert.True(IsAllowed(StrategyModel.StopWhenVolatility, closePrice: 110.0, fastRma: 100.0, slowRma: 100.0,
                SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(StrategyModel.Strong)]
        [InlineData(StrategyModel.Weak)]
        [InlineData(StrategyModel.StopWhenVolatility)]
        public void rejects_an_unknown_side(StrategyModel strategy) {
            Assert.False(IsAllowed(strategy, closePrice: 110.0, FastRma, SlowRma, SignalSideModel.None));
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
