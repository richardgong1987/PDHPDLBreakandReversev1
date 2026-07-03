# RiskUtil Design

## 1. Business Purpose

Position sizing is the rule that decides *how much* to trade. Trading a fixed lot size ignores
account size and stop distance, so a single losing trade can cost wildly different amounts of
money. `RiskUtil` answers one question: given how much of the account I am willing to lose and
where my stop is, how large a position keeps that loss within budget — while respecting the
broker's tradable volume constraints.

## 2. Use Case

> Given account equity, a risk percentage, an entry price, and a stop price, calculate the
> tradable position volume whose worst-case loss (price reaching the stop) is approximately the
> chosen percentage of equity.

## 3. Input Model

`CalcVolumeByRisk` parameters:

| Input        | Meaning                                              |
| ------------ | --------------------------------------------------- |
| `equity`     | Account equity (money).                              |
| `riskPct`    | Percentage of equity to risk (e.g. `1.0` = 1%).     |
| `entry`      | Planned entry price.                                 |
| `stop`       | Stop-loss price.                                     |
| `tickSize`   | Instrument's minimum price increment.               |
| `tickValue`  | Money gained/lost per tick per lot.                 |
| `minVolume`  | Broker's minimum tradable volume.                   |
| `maxVolume`  | Broker's maximum tradable volume.                   |
| `volumeStep` | Broker's volume increment.                          |

## 4. Output Model

A single `double` — the volume to trade, already snapped to `volumeStep` and bounded by
`[minVolume, maxVolume]`. Returns `0.0` to mean **"do not trade"** whenever the inputs are
degenerate or the computed size is below the broker minimum.

## 5. Domain Rules

These rules are true regardless of any framework, broker API, or UI:

1. Risk money = `equity * riskPct / 100`.
2. Loss per lot = `|entry - stop| / tickSize * tickValue`.
3. Raw volume = risk money / loss per lot.
4. Volume must be floored to the broker's `volumeStep`.
5. A stepped volume below `minVolume` is not tradable → result is `0`.
6. A stepped volume above `maxVolume` is clamped to `maxVolume`.
7. Any non-positive or contradictory input (zero equity, zero risk, stop == entry,
   non-positive tick size/value/step) yields `0` — never a negative or undefined size.

## 6. Application Flow

`CalcVolumeByRisk` composes the smaller rules:

1. `CalcRiskMoney(equity, riskPct)` → risk budget.
2. `CalcLossPerLot(entry, stop, tickSize, tickValue)` → cost of one lot if stopped out.
3. If either is `0`, return `0`.
4. Divide risk budget by loss per lot → raw volume.
5. `NormalizeVolume(raw, minVolume, maxVolume, volumeStep)` →
   `FloorToStep` then below-min check then `Clamp`.

## 7. Architecture Boundary

- **Domain (this class):** all of `RiskUtil`. Pure functions, no state, no I/O.
- **Application (future cBot use case):** reads live values from cTrader and calls
  `RiskUtil`, then decides whether/how to place the order.
- **Infrastructure (cAlgo framework):** supplies `Account.Equity`, `Symbol.TickSize`,
  `Symbol.TickValue`, `Symbol.VolumeInUnitsMin/Max/Step`, and `ExecuteMarketOrder`.

The dependency points inward: the cBot depends on `RiskUtil`; `RiskUtil` depends on nothing.

## 8. Dependencies

- `System` (for `System.Math`) only. No `cAlgo.API`, no I/O, no time, no logging.

## 9. External Details

None. The class is deliberately free of the cTrader framework so it can be unit-tested
without a running cBot or market connection. The broker-specific numbers (tick size, volume
step, etc.) enter as plain `double` parameters supplied by the caller.

## 10. Test Strategy

Pure domain unit tests with xUnit in `tests/Pdhpdl.Tests`. The test project links
`Risk/RiskUtil.cs` directly (`<Compile Include>`) instead of referencing the cBot project, so
tests never load `cTrader.Automate`. Coverage:

- Each helper in isolation (`FloorToStep`, `Clamp`, `CalcRiskMoney`, `CalcLossPerLot`,
  `NormalizeVolume`).
- The full `CalcVolumeByRisk` path with a worked numeric example.
- Every `0`-returning guard clause and the below-minimum / clamp-to-max boundaries.

See `docs/testing.md` for how to run them.

## 11. Risks and Trade-offs

- **Units vs. lots.** The tick math assumes `volume` is in lots. cTrader expresses volume in
  *units*; if the caller sizes in units, the `tickValue` passed must be the per-unit value so
  the result comes out in units. The caller owns this conversion.
- **Floating-point flooring.** `FloorToStep` uses `Math.Floor(value / step) * step`. A raw
  volume sitting exactly on a step boundary could floor to the step below due to binary
  rounding; acceptable here because under-sizing is the safe direction for risk.
- **`riskPct` units.** It is a percent (`1.0` = 1%), not a fraction (`0.01`). Mislabeling it
  would size 100× off. Named explicitly in the design and tests to prevent this.
