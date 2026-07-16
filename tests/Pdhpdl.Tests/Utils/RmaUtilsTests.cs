using System;
using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Utils {
    public class RmaUtilsTests {
        [Theory]
        [InlineData(99.0, 100.0, RmaPositionModel.FastBelowSlow)]
        [InlineData(100.0, 100.0, RmaPositionModel.Equal)]
        [InlineData(101.0, 100.0, RmaPositionModel.FastAboveSlow)]
        public void returns_fast_rma_position_relative_to_slow_rma(double fastRma, double slowRma, RmaPositionModel expected) {
            Assert.Equal(expected, RmaUtils.GetFastToSlowPosition(fastRma, slowRma));
        }

        [Theory]
        [InlineData(double.NaN, 100.0)]
        [InlineData(100.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 100.0)]
        [InlineData(100.0, double.NegativeInfinity)]
        public void rejects_invalid_values(double fastRma, double slowRma) {
            Assert.Throws<ArgumentException>(() => RmaUtils.GetFastToSlowPosition(fastRma, slowRma));
        }
    }
}
