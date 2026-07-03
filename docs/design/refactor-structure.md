# PDH/PDL Break & Reverse — Structural Refactor Design

## 1. Business Purpose

The cBot trades false breakouts of the Previous Day High (PDH) and Previous Day
Low (PDL). This refactor does **not** change any trading behavior. It reorganizes
the code so responsibilities are separated, the position-sizing math becomes unit
testable, and folder names describe what they hold.

## 2. Use Case

On each closed bar: detect a PDH/PDL false-breakout signal, size an order against a
fixed risk budget, place it through cTrader, and record the trade to CSV.

## 3. Responsibility Map (target)

| Concern | Type | cAlgo-coupled? | Testable? |
| --- | --- | --- | --- |
| Signal rules (`high/low/close` vs `PDH/PDL`) | `PdhpdlSignalRules` (pure) | no | yes |
| Read bars into a signal | `PdhpdlSignalDetector` | yes (reads `Bars`) | no |
| Position sizing / order geometry | `PdhpdlOrderPlanner` (pure) | no | yes |
| Symbol facts the planner needs | `IPdhpdlSymbol` port | no | fakeable |
| Real cTrader symbol | `CAlgoSymbol` adapter | yes | n/a |
| Place & track orders | `PdhpdlOrderExecutor` | yes | no |
| Time / news / risk windows | `PdhpdlRiskGuard` (pure) | no | yes |
| Draw lines / markers | `PdhpdlLines`, `PdhpdlSignalMarkers` | yes | no |
| Write CSV | `PdhpdlTradeCsvLogger` | yes (file IO) | no |

## 4. Dependency Direction

```
Robot (composition root)
  -> PdhpdlSignalDetector -> PdhpdlSignalRules (pure)
  -> PdhpdlOrderExecutor  -> PdhpdlOrderPlanner (pure) -> IPdhpdlSymbol (port)
                          -> PdhpdlRiskGuard (pure)     ^-- CAlgoSymbol (adapter)
                          -> PdhpdlTradeCsvLogger
```

Pure classes never import `cAlgo.API`. The planner talks to the broker only through
`IPdhpdlSymbol`, so it can be sized and asserted in tests with a fake symbol.

## 5. Key Design Choices

- **`PdhpdlTradeDirection { Long, Short }`** replaces `cAlgo.API.TradeType` inside the
  plan/planner, so the sizing math carries no cAlgo dependency. The executor maps it
  to `TradeType` at the broker boundary only.
- **`GetDaysToDraw`** moves from the `PdhpdlUtils` grab-bag into `PdhpdlLines`, its only
  caller (a drawing concern).
- The `PdhpdlUtils` catch-all and the misleadingly named `OrderUtil/` and `models/`
  folders are removed. Files live beside the feature they serve.

## 6. Folder Layout (target)

```
Signals/     PdhpdlSignal, PdhpdlSignalRules (pure), PdhpdlSignalDetector
Orders/      PdhpdlTradeDirection, PdhpdlEntryMode, PdhpdlOrderPlan,
             IPdhpdlSymbol, PdhpdlOrderPlanner (all pure), CAlgoSymbol, PdhpdlOrderExecutor
Risk/        RiskUtil, PdhpdlRiskGuard, PdhpdlRiskGuardConfig, NewsBlackoutWindow (pure)
LineDrawer/  PdhpdlLines, PdhpdlSignalMarkers
OrderLogger/ PdhpdlTradeCsvLogger, PdhpdlTradeCsvRecord
```

## 7. Test Strategy

The `RiskUtil.Tests` project links pure source files directly (no cAlgo). This refactor
adds two link groups and two test classes:

- `PdhpdlSignalRules` — long/short predicate truth tables.
- `PdhpdlOrderPlanner` (with a `FakeSymbol : IPdhpdlSymbol`) — volume capping, min/max
  rejection, and the risk-money cap that was flagged for oversizing.

## 8. Risks and Trade-offs

- The sizing math is delicate and was previously flagged for oversizing. It is **moved
  verbatim**, not altered — tests are added around it to lock current behavior.
- Introducing `PdhpdlTradeDirection` + `IPdhpdlSymbol` is a small abstraction, justified
  solely because it makes the flagged sizing code testable. No ports/adapters framework
  beyond that is introduced (per project preference for simple C#).
