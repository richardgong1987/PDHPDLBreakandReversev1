# Position Sizing Design (formerly RiskUtil)

> **Status note.** Sections 3–10 describe the original standalone `RiskUtil.CalcVolumeByRisk`
> design, which has since been superseded and removed. Position sizing now lives in
> `PdhpdlOrderPlanner` (against the `IPdhpdlSymbolModel` port); the risk-money calculation was
> merged into `PdhpdlRiskGuard.CalculateRiskMoney`, and the `RiskUtil` class no longer exists.
> The **current, authoritative sizing rule and its correction** are in §12 — read that first.

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
riskMoney   = equity * riskPct / 100            (PdhpdlRiskGuard.CalculateRiskMoney, x safety factor)
stopLossPips = riskPrice / pipSize              (riskPrice = |entry - stop| in price)
lossPerUnit = stopLossPips * pipValue           (pipValue = account-currency value of a pip)
idealVolume = riskMoney / lossPerUnit
volume      = NormalizeVolumeInUnits(idealVolume)   // nearest tradable step
reject if volume < VolumeInUnitsMin or > VolumeInUnitsMax
```

`pipValue` is the deposit-currency value of one pip for one unit, so `volume * lossPerUnit ≈
riskMoney` **in the account currency** — a stop-out loses ≈ `riskPct`% of equity regardless of
what currency the instrument is quoted in.

**The two bugs that were fixed (in order):**

1. **Min-of-two + round-down (fixed first).** The earliest version took `Math.Min` of the
   broker's `VolumeForProportionalRisk` and `riskMoney / riskPrice`, then rounded with
   `RoundingMode.Down`. Both only shrink the position. Replaced with a single formula rounded to
   the *nearest* step.
2. **Currency conversion (this fix).** `riskMoney / riskPrice` divides an account-currency
   budget by a *quote-currency* price distance. For the test account — **EUR deposit, USD-quoted
   XAUUSD** (1 USD ≈ 0.855 EUR, confirmed from `report.html`: `depositAsset: EUR`, and every
   trade's gross = price-move × volume × 0.855) — that sized every position ~15% too small.
   Using `pipValue` folds in the USD→EUR conversion, so the budget is spent in the currency it
   is measured in.

Diagnostic tell (visible in `log.txt`): the plan logged `RiskMoney` (the target) far above
`EstimatedRiskMoney` (`Symbol.AmountRisked`, the *true* account-currency risk). Trade 1:
`RiskMoney 90` vs `EstimatedRiskMoney 73` — the ~0.855 gap is exactly the conversion. After the
fix the sized position makes those two agree.

**What the shortfall was made of** (why a −1% target realized ≈ −0.75% before this fix):
- **Currency conversion ~0.855** — the bug above. Fixed.
- **Safety factor 0.9** — `RiskSafetyFactor` is a user parameter; it deliberately targets 0.9%.
  Set it to `1.0` for a full 1%.
- **Integer step** — a wide stop makes 1% worth only ~1–2 units, so nearest-rounding can't hit
  the budget exactly. Irreducible with whole-unit volume steps; worst on large-stop trades.

Note: the stop itself is **not** hit early — trade 1's stop was planned at 4405.04 and filled at
4405.03. The shortfall was position size, not stop placement.

The residual after the currency fix is only the safety factor and integer rounding. Sizing does
not otherwise compensate. If a run wants realized loss
centered exactly on 1%, that would be an explicit opt-in knob, not the default.

### 12.1 Residual loss distribution — why the tails are irreducible

Measured on the currency-correct run (89 stop-outs, `RiskSafetyFactor` 0.9):

| stat | value |
| --- | --- |
| mean, median loss / entry equity | ~0.95% |
| range | 0.82% – 1.13% |
| within 0.90%–1.10% | 81% (72/89) |

The band is centered on target; **the tails are not sizing bugs** and cannot be narrowed by
sizing:

- **High tail (2 trades > 1.10%)** — stop-loss slippage. Trade 123: stop planned at 4553.38,
  filled at 4555.55 (+2.15 pts / +22% past the 9.70 stop). Intended risk was 0.92% and the size
  (13 units) was exactly correct; the market ran through the stop. `RiskSafetyFactor` is the
  buffer that keeps such overruns near 1% instead of higher.
- **Low tail (15 trades < 0.90%)** — integer-lot granularity on wide-stop trades. A far stop
  (e.g. riskPrice 33) makes 0.9% worth only ~3 whole units; 3 = 0.83%, 4 = 1.11%, and
  nearest-rounding picks the closer. Fractional lots are not tradable.

`RiskSafetyFactor` **shifts the center** of the band (it does not narrow it): 0.9 lands realized
at ~0.95% (it already absorbs the ~6% average exit slippage); ~0.95 centers it on 1.0% but
fattens the high tail. The spread itself is set by integer lots (down) and market-stop slippage
(up) — do **not** change the sizing math to chase it.

## 13. Trade-log reset

The CSV at `~/Documents/pdhpdl-trades.csv` is a single fixed, append-only file. Without a reset
every backtest run stacks another full copy of the (deterministic) trades — the raw file grew
to ~8 copies, mixing pre- and post-fix runs and making it unreadable. `PdhpdlTradeCsvLogger`
now overwrites the file with a fresh header at construction when `resetOnStart` is true (the
`启动时清空交易记录CSV` / "reset trade log on start" parameter, default on), so the file always
reflects the latest run.
