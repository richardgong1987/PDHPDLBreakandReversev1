using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Utils {
    public class RmaUtilsTests {
        [Theory]
        [InlineData(99.0, 100.0, true)]
        [InlineData(100.0, 100.0, false)]
        [InlineData(101.0, 100.0, false)]
        public void identifies_when_fast_rma_is_below_slow_rma(double fastRma, double slowRma, bool expected) {
            Assert.Equal(expected, RmaUtils.IsFastBelowSlow(fastRma, slowRma));
        }

        [Theory]
        [InlineData(double.NaN, 100.0)]
        [InlineData(100.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 100.0)]
        [InlineData(100.0, double.NegativeInfinity)]
        public void returns_false_for_invalid_values(double fastRma, double slowRma) {
            Assert.False(RmaUtils.IsFastBelowSlow(fastRma, slowRma));
        }
    }
}
