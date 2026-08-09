# Pending Order Expiry Design

## 1. Business Purpose

A big-K signal (bar range > 3×ATR14) is never entered at the close. The planner switches the
entry to a limit order at the midpoint between the stop and the bar's favourable extreme, and
waits for price to pull back there.

If the pullback never comes, the market has moved on but the order stays parked at a price
that is no longer where the setup was valid. When price finally drifts back — possibly hours
later, on an unrelated move — the order fills on a stale premise. It also occupies the
"one open order per symbol" slot, blocking fresh signals.

The order should therefore only live as long as the setup stays fresh.

## 2. Use Case

A pending order that has not filled after 3 closed bars is cancelled.

## 3. Input Model

- `PdhpdlOrderExecutor.PendingOrderExpiryBars` — fixed at `3`, deliberately not a robot
  parameter: it is a strategy rule, not a knob to optimize.
- `closedBarIndex` — the index of the bar that just closed, passed on every `OnBar`.
- `PdhpdlOrderPlanModel.SignalBarIndex` — the closed bar that produced the plan, recorded when
  the pending order is submitted.

## 4. Output Model

None. The effect is a cancelled pending order plus a `*****Pending order cancelled` log line.

## 5. Domain Rules

- An order placed on the close of bar `i` is cancelled at the close of bar `i + 3`, so it gets
  exactly three full bars to fill.
- Orders with no recorded placement bar (e.g. carried over from a previous run) are not
  touched by this rule — the bot did not observe when they were placed.
- Cancellation does not distinguish big-K orders from pullback-mode orders: every pending order
  this strategy places is a "wait for a pullback" order and ages the same way.

## 6. Application Flow

`OnBar`:

1. `ManageOpenPositions` — existing risk-guard force close.
2. `CancelExpiredPendingOrders(Bars.Count - 2)`
   - forget bookkeeping for orders that are no longer pending (they filled),
   - cancel each strategy pending order whose age in bars has reached the limit.
3. `HandleClosedBarSignal` — evaluate the new signal.

Expiry runs **before** signal evaluation so a just-expired order no longer blocks a new entry
via the "symbol already has a pending order" gate.

## 7. Architecture Boundary

- Adapter/infrastructure: `PdhpdlOrderExecutor` — it owns the cAlgo `PendingOrders` collection,
  the bar limit constant, and the cancel call. The rule lives here because it is bookkeeping
  over live broker state, not a pattern rule.
- Composition root: the robot only calls `CancelExpiredPendingOrders` from `OnBar`.
- No change to the pure layers (`PdhpdlOrderPlanner`, `PdhpdlRiskGuard`, `MainBiz`).

## 8. Dependencies

`PDHPDLBreakandReversev1.cs` → `PdhpdlOrderExecutor` → cAlgo `Robot`.

## 9. External Details

cTrader `Robot.PendingOrders` and `Robot.CancelPendingOrder`. Bar index comes from
`Bars.Count - 2`, the same closed-bar convention `PdhpdlSignalDetector` uses.

## 10. Test Strategy

Not unit tested: the rule reads and mutates live cAlgo order state, so it cannot be exercised
without the framework. It is validated in the backtester — an expired order prints
`*****Pending order cancelled | Order: <id>, Reason: unfilled after 3 bars`, and the trades
CSV shows the pending ENTRY row with no matching close row.

## 11. Risks and Trade-offs

- **Bar index vs. wall clock.** Expiry is counted in bars, not in `ExpirationTime` on the order
  itself. Bars are what the strategy reasons in, and a bar count behaves identically across
  weekends and session gaps, where a wall-clock expiry would not.
- **Cancelled orders leave an orphan CSV row.** `AppendPendingEntry` already wrote an ENTRY row
  that will never receive a close row. This matches the existing behaviour of the risk-guard
  cancel path; no cancellation record type was added.
- **Label-keyed bookkeeping.** Pending CSV ids are keyed by label, and labels are per-side, not
  per-order. On cancel, the label entries are dropped only if no other pending order still
  carries that label — otherwise a `MultiplePosition` run would lose a live order's CSV id.
