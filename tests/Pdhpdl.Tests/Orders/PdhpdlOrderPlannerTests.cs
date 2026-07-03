using System;
using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Orders {
    public class PdhpdlOrderPlannerTests {
        // A signal at Close 100 with a 2-point stop distance and a 15-tick (0.15) offset.
        // Long:  stop = Low - 0.15,  risk = entry - stop.
        // Short: stop = High + 0.15, risk = stop - entry.

        [Fact]
        public void sizes_long_close_entry_and_caps_volume_by_risk_money() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryMode.Close);
            PdhpdlSignal signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlan plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(PdhpdlTradeDirection.Long, plan.Direction);
            Assert.True(plan.IsMarketOrder);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(97.85, plan.StopPrice, precision: 6);
            Assert.Equal(2.15, plan.RiskPrice, precision: 6);
            Assert.Equal(104.30, plan.TakeProfitPrice, precision: 6);
            Assert.Equal(21.5, plan.StopLossPips, precision: 6);

            // riskMoney = 10000 * 1% = 100; priceRiskCap = floor(100 / 2.15) = 46 < native 1000.
            Assert.Equal(1000.0, plan.NativeRiskVolumeInUnits, precision: 6);
            Assert.Equal(46.0, plan.PriceRiskCappedVolumeInUnits, precision: 6);
            Assert.Equal(46.0, plan.TotalVolumeInUnits, precision: 6);
            Assert.Equal(0.46, plan.TotalLots, precision: 6);
            Assert.True(plan.TotalVolumeInUnits * plan.RiskPrice <= plan.RiskMoney);
        }

        [Fact]
        public void sizes_short_close_entry_geometry() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryMode.Close);
            PdhpdlSignal signal = ShortSignal(close: 100.0, low: 99.0, high: 101.0);

            PdhpdlOrderPlan plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(PdhpdlTradeDirection.Short, plan.Direction);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(101.15, plan.StopPrice, precision: 6);
            Assert.Equal(1.15, plan.RiskPrice, precision: 6);
            Assert.Equal(97.70, plan.TakeProfitPrice, precision: 6);
        }

        [Fact]
        public void pullback_entry_moves_entry_toward_stop_and_places_limit_order() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryMode.Pullback50);
            PdhpdlSignal signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlan plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.False(plan.IsMarketOrder);
            // distanceToStop = |100 - 97.85| = 2.15; entry = 100 - 2.15 * 0.5 = 98.925.
            Assert.Equal(98.925, plan.EntryPrice, precision: 6);
            Assert.Equal(1.075, plan.RiskPrice, precision: 6);
        }

        [Fact]
        public void rejects_when_capped_volume_is_below_broker_minimum() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryMode.Close, volumeInUnitsMin: 100.0);
            PdhpdlSignal signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlan plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("too small", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_risk_price_is_below_minimum() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryMode.Close, minRiskPrice: 5.0);
            PdhpdlSignal signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlan plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("Risk price is too small", plan.RejectReason);
        }

        private static PdhpdlOrderPlanner CreatePlanner(PdhpdlEntryMode entryMode, double volumeInUnitsMin = 1.0,
            double minRiskPrice = 0.0) {
            var symbol = new FakeSymbol {
                TickSize = 0.01,
                PipSize = 0.1,
                LotSize = 100.0,
                VolumeInUnitsMin = volumeInUnitsMin,
                VolumeInUnitsMax = 1_000_000.0,
                ProportionalRiskVolume = 1000.0
            };
            var guard = new PdhpdlRiskGuard(new PdhpdlRiskGuardConfig { RiskSafetyFactor = 1.0, MinRiskPrice = minRiskPrice });
            return new PdhpdlOrderPlanner(symbol, guard, stopOffsetTicks: 15, takeProfitR: 2.0, entryMode, riskPct: 1.0);
        }

        private static PdhpdlSignal LongSignal(double close, double low, double high) {
            return new PdhpdlSignal { HasData = true, IsLongSignal = true, Close = close, Low = low, High = high };
        }

        private static PdhpdlSignal ShortSignal(double close, double low, double high) {
            return new PdhpdlSignal { HasData = true, IsShortSignal = true, Close = close, Low = low, High = high };
        }

        // Deterministic stand-in for a cTrader Symbol: volumes floor to whole units.
        private sealed class FakeSymbol : IPdhpdlSymbol {
            public double TickSize { get; set; }
            public double PipSize { get; set; }
            public double LotSize { get; set; }
            public double VolumeInUnitsMin { get; set; }
            public double VolumeInUnitsMax { get; set; }
            public double ProportionalRiskVolume { get; set; }

            public double NormalizeVolumeInUnits(double volumeInUnits) => Math.Floor(volumeInUnits);
            public double VolumeForProportionalRisk(double riskPct, double stopLossPips) => ProportionalRiskVolume;
            public double AmountRisked(double volumeInUnits, double stopLossPips) => volumeInUnits;
        }
    }
}
