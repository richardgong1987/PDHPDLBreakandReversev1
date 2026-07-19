# Strategy Mode: Signal Isolation Design

## 1. Business Purpose

Backtesting the PDH/PDL strategy needs a way to isolate a single candle-signal
family (Pinbar, Engulf, FractalTop, FractalBottom, Harami) in a single direction,
so each signal's edge can be measured on its own — not only the full combined
strategy.

## 2. Use Case

The "策略模式" (`StrategyModel`) parameter selects either a **production mode**
(all signals run together) or an **isolation test mode** (one signal family, one
direction).

## 3. Input Model

`StrategyModel` enum, read from the cBot `[Parameter("策略模式")]`:

- Production: `All`, `Reversal`, `Continuation`
- Isolation (both key levels): `PinbarLong/Short`, `EngulfLong/Short`,
  `HaramiLong/Short`, `FractalTopShort`, `FractalBottomLong`
- Isolation + key level: the same 8 combos prefixed `Pdh…` and `Pdl…`
  (e.g. `PdhPinbarShort`, `PdlFractalTopShort`) — 16 more.

FractalTop is only bearish (`ShortTop`) and FractalBottom is only bullish
(`LongBottom`); there is no LongTop / ShortBottom pattern, so those combos are not
offered.

Key-level ↔ branch mapping (a level maps to a different breakout branch per
direction): Short+PDH and Long+PDL are the 假突破/反转 branch; Short+PDL and
Long+PDH are the 真突破/延续 branch. `StrategyModePolicy` encodes this once so the
signal predicates in `MainBiz` are never touched by the key-level axis.

## 4. Output Model

No new output. The mode gates which of `signalModel.IsLongSignal` /
`IsShortSignal` and which family predicates may fire.

## 5. Domain Rules (four orthogonal switches)

| Switch | Production | Isolation test |
| --- | --- | --- |
| Direction (作多/作空) | both (RMA filtered) | only the mode's bound direction |
| Signal family | all families | only the mode's bound family |
| Breakout branch (假突破 A / 真突破 B) | `All`=both, `Reversal`=A only, `Continuation`=B only | both, unless a key level is bound |
| Key level (PDH/PDL) | both | `Pdh…`/`Pdl…` modes run only the branch touching that level |

`StrategyModePolicy` translates the enum into these switches via a spec table
(`IsolationSpecs`). It is the one place that knows the mode taxonomy. The key-level
switch is expressed through the breakout-branch gates (see the level↔branch mapping
above), so no predicate code changes when a key level is bound.

## 6. Application Flow

`MainBiz.IsShortSignal` / `IsLongSignal`:
1. require RMA data
2. `AllowsShort/AllowsLong(mode)` — direction gate (isolation)
3. RMA position gate (unchanged)
4. for each family: `AllowsFamily(mode, family) && <FamilyPredicate>()`

Each family predicate still gates its two breakout branches with
`AllowsReversal` / `AllowsContinuation` (formerly `Utils.CanA` / `Utils.CanB`).

## 7. Architecture Boundary

- Domain/pure: `StrategyModel`, `SignalFamilyModel`, `StrategyModePolicy`,
  `MainBiz`, `Utils` — no `cAlgo.API`.
- Adapter: the `[Parameter]` property in the Robot shell feeds the enum inward.

## 8. Dependencies

`StrategyModePolicy` depends only on `StrategyModel`, `SignalFamilyModel`,
`PdhpdlTradeDirectionModel`. `MainBiz` depends on `StrategyModePolicy`.

## 9. External Details

None. Pure logic, unit-tested.

## 10. Test Strategy

`StrategyModePolicyTests`: production modes allow all families + both directions;
`Reversal`/`Continuation` toggle exactly one breakout branch; each isolation mode
allows only its bound family + direction and keeps both breakout branches.

## 11. Risks and Trade-offs

The enum mixes two axes (breakout-type production modes vs family×direction test
modes) in one dropdown. This is intentional: the cTrader optimizer sweeps a single
enum axis easily, and the heterogeneity is contained inside `StrategyModePolicy`,
keeping `MainBiz` readable.
