# RiskUtil Design

> **Status note.** Sections 3–10 describe the original standalone `RiskUtil.CalcVolumeByRisk`
> design. That logic now lives in `PdhpdlOrderPlanner` (which sizes against the
> `IPdhpdlSymbolModel` port); `RiskUtil` today holds only `CalcRiskMoney`. The **current,
> authoritative sizing rule and its correction** are in §12 — read that first.

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

## 12. Sizing correction (current implementation)

**Where:** `PdhpdlOrderPlanner.CreatePlan`. This is the authoritative sizing rule.

**Rule:**

```
riskMoney   = equity * riskPct / 100            (RiskUtil.CalcRiskMoney, x safety factor)
idealVolume = riskMoney / riskPrice             (riskPrice = |entry - stop| in price)
volume      = NormalizeVolumeInUnits(idealVolume)   // nearest tradable step
reject if volume < VolumeInUnitsMin or > VolumeInUnitsMax
```

By construction `volume * riskPrice ≈ riskMoney`, so a stop-out loses ≈ `riskPct`% of equity.

**The bug that was fixed.** The earlier version computed *two* volumes — the broker's
`VolumeForProportionalRisk` and `riskMoney / riskPrice` — took `Math.Min` of them, and rounded
with `RoundingMode.Down`. Both choices only ever shrink the position, so realized stop-out
losses came in **well under** the 1% budget. Backtest evidence (XAUUSD M15, 89 stop-outs),
measured on the clean post-fix run:

| Metric | Before | After |
| --- | --- | --- |
| avg realized loss / 1% target | **0.75** | **0.82** |
| worst single trade (large stop, ~2 units) | **0.41** | **0.82** |

The undersizing was worst for small positions, where `Down`-rounding discards up to a whole
unit (e.g. ideal 2.13 units → 1, halving the risk). Taking the *nearest* step and dropping the
redundant `Min`/`VolumeForProportionalRisk` cap centers *intended* risk on the budget.

**Residual gap (~18%, not a sizing bug).** Two effects remain, neither of which sizing should
chase:
1. **Execution (~13%).** Entries are market orders; on losing trades the fill/spread makes the
   realized entry-to-stop loss come in below the planned `riskPrice`. "Risk 1%" is defined as
   *intended* risk (stop at the planned level) = 1%; realized loss is naturally ≤ that.
2. **Integer step (~5%).** A wide stop makes 1% only worth ~1–2 units, so nearest-rounding
   still can't hit the budget exactly (2.13 → 2). Distribution of realized/target after the
   fix: min 0.55, median 0.82, max 1.02.

Compensating by sizing up (e.g. dividing `riskMoney` by the ~0.87 execution factor) would push
cleanly-stopped trades over 1%, so it is deliberately not done. If a run wants realized loss
centered exactly on 1%, that would be an explicit opt-in knob, not the default.

## 13. Trade-log reset

The CSV at `~/Documents/pdhpdl-trades.csv` is a single fixed, append-only file. Without a reset
every backtest run stacks another full copy of the (deterministic) trades — the raw file grew
to ~8 copies, mixing pre- and post-fix runs and making it unreadable. `PdhpdlTradeCsvLogger`
now overwrites the file with a fresh header at construction when `resetOnStart` is true (the
`启动时清空交易记录CSV` parameter, default on), so the file always reflects the latest run.
