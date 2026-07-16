# HanJin 26 Candle Signals Design

## 1. Business Purpose

Port the Pine Script library `HanJinSignals26` (7 candlestick patterns) to a pure,
framework-independent C# utility so the cBot can classify a candle (or short window of
candles) into a trade side without depending on cAlgo, TradingView, or any indicator engine.

## 2. Use Case

Given the recent closed candles, tell the caller which of the 7 patterns fired and on which
side (Buy / Sell / None).

## 3. Input Model

- `CandleModel` — one candle's Open/High/Low/Close (pure value type).
- Pattern methods take the exact candles they need, named by role:
  - single-bar patterns (Pinbar, BigBody) take `current`.
  - two-bar pattern (Engulf) takes `current`, `previous`.
  - three-bar patterns (Fractal, Harami) take `current`, `previous`, `earlier`,
    mirroring Pine offsets `[0]`, `[1]`, `[2]`.
- `HanJinSignalOptionsModel` — the tunable fractions (pinbar long/short wick, strict flag,
  big-body min fraction), defaulted to the Pine defaults.

## 4. Output Model

- `SignalSideModel` — `None | Buy | Sell` (replaces Pine's `"BUY"/"SELL"/na` magic strings).
- `HanJinSignalScanModel` — the aggregate result of `Scan(...)`, one side per pattern.

## 5. Domain Rules

- **Pinbar**: on a bar with range > 0, `lowerWick/range >= longFrac` (and, if strict,
  `upperWick/range <= shortFrac`) → Buy; the mirror → Sell.
- **Engulf**: current bar's high/low and body fully cover the previous bar's → follow the
  current body direction (up → Buy, down → Sell).
- **Fractal**: the middle bar (`previous`, `[1]`) strictly dominates both neighbours on the
  high AND low line. Top fractal → Sell, bottom fractal → Buy.
- **Harami breakout**: the earlier parent bar (`[2]`) must strictly contain the previous
  inside bar (`[1]`) by both wick range and body range. The current confirmation bar (`[0]`)
  then determines the side from its **close**:
  - `current.Close > previous.High` → Buy (Harami break-up).
  - `current.Close < previous.Low` → Sell (Harami break-down).
  - Otherwise → None.
  The body direction of either the parent bar or the inside bar is irrelevant. The current
  bar does not have to be contained because it is the breakout confirmation bar.
- **BigBody**: `|close-open|/range >= minFrac` → follow the body direction.

### Harami compatibility note

`HaramiSingle` is the value consumed by `PdhpdlSignalDetector`. `HaramiDouble` is a legacy
output field and currently mirrors the same breakout side for compatibility; it is not used
to open orders.

Example: `[2]` has high/low `10/0`, `[1]` has high/low `7/2`, and `[0]` closes at `7.5`.
Because `[2]` contains `[1]` and `7.5 > 7`, the result is Buy. If `[0]` closes at `1.5`,
the result is Sell. A close at `5.5` produces None.

This is an intentional strategy-specific definition. Do **not** restore the earlier classic
Harami implementation that made `[1]` contain `[0]` and reversed the parent candle's body
direction. That behavior is obsolete and is not an entry rule for this cBot.

A candle with zero range (high == low) yields `None` for the fraction-based patterns,
matching Pine's `na` propagation.

## 6. Application Flow

`Scan` calls each pattern method and packs the sides into `HanJinSignalScanModel`. Callers
that only need one pattern call that method directly.

## 7. Architecture Boundary

All of this is **domain**: pure functions over value types, no `using cAlgo.API`. Whatever
reads cAlgo `Bars` and builds `CandleModel`s is an adapter and lives outside this module.

## 8. Dependencies

`System` only. No cAlgo, no I/O, no time.

## 9. External Details

None. This is the sole reason it is unit-testable by linking into `Pdhpdl.Tests`.

## 10. Test Strategy

xUnit unit tests over each pattern: one firing case per side plus the key negative
(no-range doji, Harami close without a breakout, non-dominant fractal).

## 11. Risks and Trade-offs

- The Pine names Buy/Sell keep the library's original meaning; this is a *classifier*, not a
  trading policy — mapping a side to an actual order stays with the cBot.
- Three-bar patterns need three candles; callers must pass them oldest-in-`earlier`.
