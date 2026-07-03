using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Signals {
    public class PdhpdlSignalRulesTests {
        // pdh = 110, pdl = 90 in every case below.
        private const double Pdh = 110.0;
        private const double Pdl = 90.0;

        [Fact]
        public void short_signal_when_bar_pierces_pdh_but_closes_back_below() {
            Assert.True(PdhpdlSignalRules.IsShortSignal(high: 112.0, open: 108.0, close: 109.0, Pdh, Pdl));
        }

        [Fact]
        public void no_short_signal_when_close_stays_above_pierced_level() {
            Assert.False(PdhpdlSignalRules.IsShortSignal(high: 112.0, open: 109.0, close: 111.0, Pdh, Pdl));
        }

        [Fact]
        public void no_short_signal_when_level_is_never_pierced() {
            Assert.False(PdhpdlSignalRules.IsShortSignal(high: 108.0, open: 105.0, close: 106.0, Pdh, Pdl));
        }

        [Fact]
        public void long_signal_when_bar_pierces_pdl_but_closes_back_above() {
            Assert.True(PdhpdlSignalRules.IsLongSignal(low: 88.0, open: 92.0, close: 91.0, Pdh, Pdl));
        }

        [Fact]
        public void no_long_signal_when_close_stays_below_pierced_level() {
            Assert.False(PdhpdlSignalRules.IsLongSignal(low: 88.0, open: 91.0, close: 89.0, Pdh, Pdl));
        }

        [Fact]
        public void no_long_signal_when_level_is_never_pierced() {
            Assert.False(PdhpdlSignalRules.IsLongSignal(low: 92.0, open: 95.0, close: 96.0, Pdh, Pdl));
        }
    }
}
