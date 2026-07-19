using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // Locks in the "策略模式" taxonomy: production modes run every family in both
    // directions; isolation modes bind exactly one family + one direction and keep
    // both breakout branches open; Reversal/Continuation toggle one breakout branch.
    public class StrategyModePolicyTests {
        [Theory]
        [InlineData(StrategyModel.All)]
        [InlineData(StrategyModel.Reversal)]
        [InlineData(StrategyModel.Continuation)]
        public void production_mode_allows_both_directions_and_every_family(StrategyModel mode) {
            Assert.True(StrategyModePolicy.AllowsLong(mode));
            Assert.True(StrategyModePolicy.AllowsShort(mode));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Pinbar));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Engulf));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalTop));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalBottom));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Harami));
        }

        [Fact]
        public void all_mode_opens_both_breakout_branches() {
            Assert.True(StrategyModePolicy.AllowsReversal(StrategyModel.All));
            Assert.True(StrategyModePolicy.AllowsContinuation(StrategyModel.All));
        }

        [Fact]
        public void reversal_mode_opens_only_the_fake_breakout_branch() {
            Assert.True(StrategyModePolicy.AllowsReversal(StrategyModel.Reversal));
            Assert.False(StrategyModePolicy.AllowsContinuation(StrategyModel.Reversal));
        }

        [Fact]
        public void continuation_mode_opens_only_the_real_breakout_branch() {
            Assert.False(StrategyModePolicy.AllowsReversal(StrategyModel.Continuation));
            Assert.True(StrategyModePolicy.AllowsContinuation(StrategyModel.Continuation));
        }

        [Fact]
        public void pinbar_long_isolation_allows_only_long_pinbar_but_both_breakout_branches() {
            StrategyModel mode = StrategyModel.PinbarLong;

            Assert.True(StrategyModePolicy.AllowsLong(mode));
            Assert.False(StrategyModePolicy.AllowsShort(mode));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Pinbar));
            Assert.False(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Engulf));
            Assert.True(StrategyModePolicy.AllowsReversal(mode));
            Assert.True(StrategyModePolicy.AllowsContinuation(mode));
        }

        [Fact]
        public void engulf_short_isolation_allows_only_short_engulf() {
            StrategyModel mode = StrategyModel.EngulfShort;

            Assert.False(StrategyModePolicy.AllowsLong(mode));
            Assert.True(StrategyModePolicy.AllowsShort(mode));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Engulf));
            Assert.False(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.Harami));
        }

        [Fact]
        public void fractal_top_isolation_is_short_only() {
            StrategyModel mode = StrategyModel.FractalTopShort;

            Assert.True(StrategyModePolicy.AllowsShort(mode));
            Assert.False(StrategyModePolicy.AllowsLong(mode));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalTop));
            Assert.False(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalBottom));
        }

        [Fact]
        public void fractal_bottom_isolation_is_long_only() {
            StrategyModel mode = StrategyModel.FractalBottomLong;

            Assert.True(StrategyModePolicy.AllowsLong(mode));
            Assert.False(StrategyModePolicy.AllowsShort(mode));
            Assert.True(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalBottom));
            Assert.False(StrategyModePolicy.AllowsFamily(mode, SignalFamilyModel.FractalTop));
        }
    }
}
