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

## 5. Domain Rules (verbatim from the Pine library)

- **Pinbar**: on a bar with range > 0, `lowerWick/range >= longFrac` (and, if strict,
  `upperWick/range <= shortFrac`) → Buy; the mirror → Sell.
- **Engulf**: current bar's high/low and body fully cover the previous bar's → follow the
  current body direction (up → Buy, down → Sell).
- **Fractal**: the middle bar (`previous`, `[1]`) strictly dominates both neighbours on the
  high AND low line. Top fractal → Sell, bottom fractal → Buy.
- **Harami**: parent bar (`[1]`) contains current (`[0]`) → single, reverse of the parent
  body. Also `[2]` contains `[1]` → double, reverse of the grandparent body. Reverse means
  up body → Sell.
- **BigBody**: `|close-open|/range >= minFrac` → follow the body direction.

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
(no-range doji, not-contained harami, non-dominant fractal).

## 11. Risks and Trade-offs

- The Pine names Buy/Sell keep the library's original meaning; this is a *classifier*, not a
  trading policy — mapping a side to an actual order stays with the cBot.
- Three-bar patterns need three candles; callers must pass them oldest-in-`earlier`.
