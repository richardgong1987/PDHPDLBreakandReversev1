using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Risk {
    public class RiskUtilTests {
        [Fact]
        public void risks_the_given_percentage_of_equity() {
            Assert.Equal(100.0, RiskUtil.CalcRiskMoney(10000.0, 1.0), precision: 10);
        }

        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(-1.0, 1.0)]
        [InlineData(10000.0, 0.0)]
        [InlineData(10000.0, -1.0)]
        public void returns_zero_for_non_positive_inputs(double equity, double riskPct) {
            Assert.Equal(0.0, RiskUtil.CalcRiskMoney(equity, riskPct));
        }
    }
}
