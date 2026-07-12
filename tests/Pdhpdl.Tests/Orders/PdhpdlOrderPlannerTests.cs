using System;
using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Orders {
    public class PdhpdlOrderPlannerTests {
        // A signal at Close 100 with a 15-tick (0.15) offset applied to the detector's SL price.
        // Long:  stop = SL - 0.15,  risk = entry - stop.
        // Short: stop = SL + 0.15,  risk = stop - entry.

        [Fact]
        public void sizes_long_close_entry_to_spend_the_full_risk_budget() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close);
            PdhpdlSignalModel signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(PdhpdlTradeDirectionModel.Long, plan.DirectionModel);
            Assert.True(plan.IsMarketOrder);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(97.85, plan.StopPrice, precision: 6);
            Assert.Equal(2.15, plan.RiskPrice, precision: 6);
            Assert.Equal(104.30, plan.TakeProfitPrice, precision: 6);
            Assert.Equal(21.5, plan.StopLossPips, precision: 6);

            // riskMoney = 10000 * 1% = 100; idealVolume = 100 / 2.15 = 46.51 -> nearest = 47.
            // (The old floor-and-take-the-min logic gave 46 or less, under-spending the budget.)
            Assert.Equal(47.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.47, plan.Lots, precision: 6);
            // Intended risk lands within one volume step of the budget, not systematically under it.
            Assert.True(Math.Abs(plan.VolumeInUnits * plan.RiskPrice - plan.RiskMoney) <= plan.RiskPrice);
        }

        [Fact]
        public void sizes_up_when_pip_value_is_below_pip_size_so_account_currency_risk_hits_the_budget() {
            // pipValue 0.0855 < pipSize 0.1 models a EUR account trading a USD-quoted instrument.
            // The old formula (riskMoney / riskPrice) ignored this and sized 47; the currency-correct
            // volume is larger so the loss measured in the account currency still equals the budget.
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close, pipValue: 0.0855);
            PdhpdlSignalModel signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            // riskMoney 100; lossPerUnit = stopLossPips 21.5 * pipValue 0.0855 = 1.83825; ideal 54.4 -> 54.
            Assert.Equal(54.0, plan.VolumeInUnits, precision: 6);
            // Account-currency risk of the sized position lands within one step of the budget.
            double accountCurrencyRisk = plan.VolumeInUnits * plan.StopLossPips * 0.0855;
            Assert.True(Math.Abs(accountCurrencyRisk - plan.RiskMoney) <= plan.StopLossPips * 0.0855);
        }

        [Fact]
        public void sizes_short_close_entry_geometry() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close);
            PdhpdlSignalModel signal = ShortSignal(close: 100.0, low: 99.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(PdhpdlTradeDirectionModel.Short, plan.DirectionModel);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(101.15, plan.StopPrice, precision: 6);
            Assert.Equal(1.15, plan.RiskPrice, precision: 6);
            Assert.Equal(97.70, plan.TakeProfitPrice, precision: 6);
        }

        [Fact]
        public void pullback_entry_moves_entry_toward_stop_and_places_limit_order() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Pb50);
            PdhpdlSignalModel signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.False(plan.IsMarketOrder);
            // distanceToStop = |100 - 97.85| = 2.15; entry = 100 - 2.15 * 0.5 = 98.925.
            Assert.Equal(98.925, plan.EntryPrice, precision: 6);
            Assert.Equal(1.075, plan.RiskPrice, precision: 6);
        }

        [Fact]
        public void rejects_when_sized_volume_is_below_broker_minimum() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close, volumeInUnitsMin: 100.0);
            PdhpdlSignalModel signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("below broker minimum", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_stop_loss_pips_are_below_minimum() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close, minStopLossPips: 22.0);
            PdhpdlSignalModel signal = LongSignal(close: 100.0, low: 98.0, high: 101.0);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("Stop loss distance is too small", plan.RejectReason);
        }

        [Fact]
        public void accepts_forex_price_distance_when_minimum_is_in_pips() {
            PdhpdlOrderPlanner planner = CreatePlanner(PdhpdlEntryModel.Close, minStopLossPips: 5.0, pipValue: 0.0001,
                tickSize: 0.00001, pipSize: 0.0001);
            PdhpdlSignalModel signal = LongSignal(close: 1.1000, low: 1.0995, high: 1.1002);

            PdhpdlOrderPlanModel plan = planner.CreatePlan(signal, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(6.5, plan.StopLossPips, precision: 6);
            Assert.True(plan.RiskPrice < 5.0);
        }

        private static PdhpdlOrderPlanner CreatePlanner(PdhpdlEntryModel entryModel, double volumeInUnitsMin = 1.0,
            double minStopLossPips = 0.0, double pipValue = 0.1, double tickSize = 0.01, double pipSize = 0.1) {
            // Default pipValue == pipSize models an instrument quoted in the account currency
            // (one price unit = one currency unit per unit of volume), so volume = riskMoney / riskPrice.
            var symbol = new FakeSymbolModel {
                TickSize = tickSize,
                PipSize = pipSize,
                LotSize = 100.0,
                VolumeInUnitsMin = volumeInUnitsMin,
                VolumeInUnitsMax = 1_000_000.0,
                PipValue = pipValue
            };
            var guard = new PdhpdlRiskGuard(new PdhpdlRiskGuardConfigModel {
                RiskSafetyFactor = 1.0,
                MinStopLossPips = minStopLossPips
            });
            return new PdhpdlOrderPlanner(symbol, guard, stopOffsetTicks: 15, takeProfitR: 2.0, entryModel, riskPct: 1.0);
        }

        private static PdhpdlSignalModel LongSignal(double close, double low, double high) {
            // Longs stop below the signal; the detector's SL price is the bar low.
            return new PdhpdlSignalModel { HasData = true, IsLongSignal = true, Close = close, Low = low, High = high, SL = low };
        }

        private static PdhpdlSignalModel ShortSignal(double close, double low, double high) {
            // Shorts stop above the signal; the detector's SL price is the bar high.
            return new PdhpdlSignalModel { HasData = true, IsShortSignal = true, Close = close, Low = low, High = high, SL = high };
        }

        // Deterministic stand-in for a cTrader Symbol: volume step is one whole unit,
        // rounded to nearest (matching RoundingMode.ToNearest in the real adapter).
        private sealed class FakeSymbolModel : IPdhpdlSymbolModel {
            public double TickSize { get; set; }
            public double PipSize { get; set; }
            public double LotSize { get; set; }
            public double VolumeInUnitsMin { get; set; }
            public double VolumeInUnitsMax { get; set; }
            public double PipValue { get; set; }

            public double NormalizeVolumeInUnits(double volumeInUnits) => Math.Round(volumeInUnits, MidpointRounding.AwayFromZero);
            public double AmountRisked(double volumeInUnits, double stopLossPips) => volumeInUnits * stopLossPips * PipValue;
        }
    }
}
