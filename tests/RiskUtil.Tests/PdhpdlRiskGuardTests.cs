using System;
using cAlgo.Robots;
using Xunit;

namespace RiskUtil.Tests {
    public class PdhpdlRiskGuardTests {
        [Fact]
        public void blocks_new_orders_during_daily_no_order_window() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 4, 0, 0)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 7, 59, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 7, 8, 0, 0)));
        }

        [Fact]
        public void forces_close_during_daily_force_close_window() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 7, 4, 29, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 7, 4, 30, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 7, 7, 59, 0)));
            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 7, 8, 0, 0)));
        }

        [Fact]
        public void blocks_friday_trading_and_forces_close_after_friday_cutoff() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 9, 0, 0, 0)));
            Assert.False(guard.ShouldForceClose(new DateTime(2026, 1, 9, 3, 29, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 9, 3, 30, 0)));
        }

        [Fact]
        public void blocks_weekend_trading_and_forces_close() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 10, 12, 0, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 10, 12, 0, 0)));
        }

        [Fact]
        public void blocks_and_forces_close_during_news_blackout() {
            PdhpdlRiskGuard guard = CreateGuard("2026-01-08 14:00~2026-01-08 15:00");

            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 13, 59, 0)));
            Assert.True(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 14, 0, 0)));
            Assert.True(guard.ShouldForceClose(new DateTime(2026, 1, 8, 14, 30, 0)));
            Assert.False(guard.ShouldBlockNewOrder(new DateTime(2026, 1, 8, 15, 0, 0)));
        }

        [Fact]
        public void calculates_percent_risk_from_current_equity_with_safety_factor() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.Equal(180.0, guard.CalculateRiskMoney(20000.0, 1.0), precision: 10);
            Assert.Equal(45.0, guard.CalculateRiskMoney(5000.0, 1.0), precision: 10);
        }

        [Fact]
        public void rejects_too_small_risk_price() {
            PdhpdlRiskGuard guard = CreateGuard();

            Assert.True(guard.TryGetRiskPriceRejectReason(3.2, out string rejectReason));
            Assert.Contains("Risk price is too small", rejectReason);
            Assert.False(guard.TryGetRiskPriceRejectReason(5.0, out _));
        }

        private static PdhpdlRiskGuard CreateGuard(string newsBlackoutWindows = "") {
            return new PdhpdlRiskGuard(new PdhpdlRiskGuardConfig {
                RiskSafetyFactor = 0.9,
                MinRiskPrice = 5.0,
                NoNewOrdersStartHour = 4,
                ForceCloseHour = 4,
                ForceCloseMinute = 30,
                ResumeTradingHour = 8,
                FridayNoNewOrdersStartHour = 0,
                FridayForceCloseHour = 3,
                FridayForceCloseMinute = 30,
                NewsBlackoutWindows = newsBlackoutWindows
            });
        }
    }
}
