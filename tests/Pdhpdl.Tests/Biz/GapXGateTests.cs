using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // 开口扩大闸门：多单要 GapX ≥ 阈值，空单要 −GapX ≥ 阈值。
    // GapX 是多头视角的带符号值，阈值只取 0 以上。
    public class GapXGateTests {
        private const double Threshold = 0.10;

        [Fact]
        public void allows_both_sides_when_gate_is_disabled() {
            Assert.True(GapXGate.IsExpanding(false, -5.0, Threshold, SignalSideModel.Buy));
            Assert.True(GapXGate.IsExpanding(false, 5.0, Threshold, SignalSideModel.Sell));
        }

        // 关掉的闸门连 GapX 都不看，暖机期也不该因此少做单。
        [Fact]
        public void allows_unavailable_gapx_when_gate_is_disabled() {
            Assert.True(GapXGate.IsExpanding(false, double.NaN, Threshold, SignalSideModel.Buy));
        }

        // 闸门开着却读不到 GapX，放行等于把这道风控静默关掉。
        [Fact]
        public void blocks_unavailable_gapx_when_gate_is_enabled() {
            Assert.False(GapXGate.IsExpanding(true, double.NaN, Threshold, SignalSideModel.Buy));
            Assert.False(GapXGate.IsExpanding(true, double.NaN, Threshold, SignalSideModel.Sell));
        }

        [Theory]
        [InlineData(0.15, true)]  // 开口在扩大，超过阈值
        [InlineData(0.10, true)]  // 门槛是「≥」，正好等于也放行
        [InlineData(0.09, false)] // 扩得不够
        [InlineData(-0.30, false)] // 开口在收窄
        public void long_needs_gapx_at_or_above_threshold(double gapX, bool expected) {
            Assert.Equal(expected, GapXGate.IsExpanding(true, gapX, Threshold, SignalSideModel.Buy));
        }

        // 空头的开口是慢线 - 快线，所以 GapX 要够负才算在扩大。
        [Theory]
        [InlineData(-0.15, true)]
        [InlineData(-0.10, true)]
        [InlineData(-0.09, false)]
        [InlineData(0.30, false)] // 多头方向在扩大，对作空是逆向的
        public void short_needs_negated_gapx_at_or_above_threshold(double gapX, bool expected) {
            Assert.Equal(expected, GapXGate.IsExpanding(true, gapX, Threshold, SignalSideModel.Sell));
        }

        // 阈值 0：只要求「没在收窄」，开口持平也放行。
        [Fact]
        public void zero_threshold_allows_a_flat_gap() {
            Assert.True(GapXGate.IsExpanding(true, 0.0, 0.0, SignalSideModel.Buy));
            Assert.True(GapXGate.IsExpanding(true, 0.0, 0.0, SignalSideModel.Sell));
            Assert.False(GapXGate.IsExpanding(true, -0.01, 0.0, SignalSideModel.Buy));
        }
    }
}
