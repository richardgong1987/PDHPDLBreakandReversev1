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
| Read bars + apply PDH/PDL rules into a signal | `PdhpdlSignalDetector` | yes (reads `Bars`) | no |
| Position sizing / order geometry | `PdhpdlOrderPlanner` (pure) | no | yes |
| Symbol facts the planner needs | `IPdhpdlSymbolModel` port | no | fakeable |
| Real cTrader symbol | `CAlgoSymbolModel` adapter | yes | n/a |
| Place & track orders | `PdhpdlOrderExecutor` | yes | no |
| Time / news / risk windows | `PdhpdlRiskGuard` (pure) | no | yes |
| Draw lines / markers | `PdhpdlLines`, `PdhpdlSignalMarkers` | yes | no |
| Write CSV | `PdhpdlTradeCsvLogger` | yes (file IO) | no |
| Upgrade an old trades CSV to the current column schema | `PdhpdlTradeCsvMigrator` (pure) | no | yes |

## 4. Dependency Direction

```
Robot (composition root)
  -> PdhpdlSignalDetector (applies PDH/PDL rules)
  -> PdhpdlOrderExecutor  -> PdhpdlOrderPlanner (pure) -> IPdhpdlSymbolModel (port)
                          -> PdhpdlRiskGuard (pure)     ^-- CAlgoSymbolModel (adapter)
                          -> PdhpdlTradeCsvLogger
```

Pure classes never import `cAlgo.API`. The planner talks to the broker only through
`IPdhpdlSymbolModel`, so it can be sized and asserted in tests with a fake symbol.

## 5. Key Design Choices

- **`PdhpdlTradeDirectionModel { Long, Short }`** replaces `cAlgo.API.TradeType` inside the
  plan/planner, so the sizing math carries no cAlgo dependency. The executor maps it
  to `TradeType` at the broker boundary only.
- **`GetDaysToDraw`** moves from the `PdhpdlUtils` grab-bag into `PdhpdlLines`, its only
  caller (a drawing concern).
- The `PdhpdlUtils` catch-all and the misleadingly named `OrderUtil/` folder are removed.
  Behavior classes live beside the feature they serve; all data types live in `Models/`
  (suffixed `Model`).
- **Writing vs. migrating the trades CSV are split.** `PdhpdlTradeCsvLogger` writes today's
  rows; `PdhpdlTradeCsvMigrator` owns the history of older column layouts and rewrites old
  files to the current schema. They change for different reasons (new field vs. reconciling an
  old on-disk format), so they are separate classes. The migrator is pure (no `cAlgo.API`), so
  it is unit-testable even though the logger — which does file IO — is not.

## 6. Folder Layout

```
Signals/     PdhpdlSignalDetector, PdhpdlSignal (data)
Orders/      PdhpdlOrderPlanner (pure), PdhpdlOrderExecutor
Risk/        PdhpdlRiskGuard (pure)
LineDrawer/  PdhpdlLines, PdhpdlSignalMarkers
OrderLogger/ PdhpdlTradeCsvLogger (write), PdhpdlTradeCsvMigrator (pure — upgrade old files)
Models/      PdhpdlOrderPlanModel, PdhpdlTradeDirectionModel, PdhpdlEntryModel,
             PdhpdlRiskGuardConfigModel, NewsBlackoutWindowModel, PdhpdlTradeCsvRecordModel,
             IPdhpdlSymbolModel (port), CAlgoSymbolModel (adapter — only Models/ file on cAlgo)
```

## 7. Test Strategy

The `Pdhpdl.Tests` project links pure source files directly (no cAlgo) — including the pure
data types in `Models/`, but never `CAlgoSymbolModel`. Test files mirror the source folders
(`Risk/`, `Signals/`, `Orders/`). The refactor adds a test class:

- `PdhpdlOrderPlanner` (with a `FakeSymbolModel : IPdhpdlSymbolModel`) — sizing to the risk
  budget and min/max rejection. (The sizing rule itself is documented in `risk-util.md`.)

## 8. Risks and Trade-offs

- The sizing math is delicate. The refactor first moved it verbatim behind tests; the sizing
  rule was **later corrected** (see `risk-util.md` §"Sizing correction") because it
  under-spent the risk budget.
- Introducing `PdhpdlTradeDirectionModel` + `IPdhpdlSymbolModel` is a small abstraction,
  justified solely because it makes the flagged sizing code testable. No ports/adapters
  framework beyond that is introduced (per project preference for simple C#).
