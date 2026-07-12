using System;
using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Risk {
    public class PdhpdlRiskGuardTests {
        [Fact]
        public void allows_new_orders_on_weekdays_outside_news_blackout() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 4, 0, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 7, 59, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 8, 0, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 16, 30, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 9, 0, 0, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 9, 9, 30, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 9, 23, 59, 59)));
        }

        [Fact]
        public void forces_close_on_saturday_at_or_after_cutoff() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 10, 5, 29, 59)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 10, 5, 30, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 10, 12, 0, 0)));
        }

        [Fact]
        public void does_not_force_close_on_weekdays_or_sunday() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 7, 4, 30, 0)));
            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 9, 23, 59, 59)));
            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 11, 12, 0, 0)));
        }

        [Fact]
        public void allows_saturday_orders_before_cutoff_and_blocks_after_cutoff() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 10, 5, 29, 59)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 10, 5, 30, 0)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 10, 12, 0, 0)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 11, 12, 0, 0)));
        }

        [Fact]
        public void blocks_new_orders_and_forces_close_during_news_blackout() {
            PdhpdlRiskGuard guard = CreateGuard("2026-01-08 14:00~2026-01-08 15:00");

            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 13, 59, 0)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 14, 0, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 8, 14, 30, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 15, 0, 0)));
            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 8, 15, 0, 0)));
        }

        [Fact]
        public void calculates_percent_risk_from_current_equity() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.Equal(200.0, guard.CalculateRiskMoney(20000.0, 1.0), precision: 10);
            Assert.Equal(50.0, guard.CalculateRiskMoney(5000.0, 1.0), precision: 10);
        }

        [Fact]
        public void applies_safety_factor_when_configured_below_one() {
            PdhpdlRiskGuard guard = CreateGuard(riskSafetyFactor: 0.9);

            Assert.Equal(180.0, guard.CalculateRiskMoney(20000.0, 1.0), precision: 10);
            Assert.Equal(45.0, guard.CalculateRiskMoney(5000.0, 1.0), precision: 10);
        }

        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(-1.0, 1.0)]
        [InlineData(20000.0, 0.0)]
        [InlineData(20000.0, -1.0)]
        public void risk_money_is_zero_for_non_positive_inputs(double equity, double riskPct) {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.Equal(0.0, guard.CalculateRiskMoney(equity, riskPct));
        }

        [Fact]
        public void rejects_too_small_stop_loss_pips() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.True(guard.TryGetStopLossPipsRejectReason(3.2, out string rejectReason));
            Assert.Contains("Stop loss distance is too small", rejectReason);
            Assert.False(guard.TryGetStopLossPipsRejectReason(5.0, out _));
        }

        private static PdhpdlRiskGuard CreateGuard(string newsBlackoutWindows = "", double riskSafetyFactor = 1.0) {
            return new PdhpdlRiskGuard(new PdhpdlRiskGuardConfigModel {
                RiskSafetyFactor = riskSafetyFactor,
                MinStopLossPips = 5.0,
                SaturdayForceCloseHour = 5,
                SaturdayForceCloseMinute = 30,
                NewsBlackoutWindows = newsBlackoutWindows
            });
        }
    }
}
