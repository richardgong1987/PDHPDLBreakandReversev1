# Consecutive Loss Counter Design

## 1. Business Purpose

Knowing how many trades in a row have lost is the first input to any "back off after a bad run"
rule. Before committing to such a rule we want to observe the streak on real backtests, so this
feature only measures and reports it — it never blocks a trade.

## 2. Use Case

After every strategy position closes, the bot reports how many trades have lost in a row up to
and including that trade.

## 3. Input Model

- `Position.NetProfit` of each closed strategy position, handed in by `OnPositionClosed`.

## 4. Output Model

- `ConsecutiveLossCounter.Count` — the current streak length.
- A `Print` line on each close: `*****Consecutive losses | Count: {n}`.

Nothing else consumes the count. No signal, plan, or order decision reads it.

## 5. Domain Rules

- The streak is **global**: every closed strategy position feeds the same counter, regardless of
  key level, direction, or entry mode.
- `NetProfit < 0` extends the streak by one. Anything else — profit or exact breakeven — resets
  it to zero. This matches the `盈利 / 亏损` split the trade CSV already writes, so the log and
  the CSV never disagree about what counts as a loss.
- The count is reported after the trade it includes, so a first loss reports `1`.

Example:

```text
亏 → 1
亏 → 2
赢 → 0
亏 → 1
```

## 6. Application Flow

1. `PdhpdlOrderExecutor.OnPositionClosed` writes the CSV close record as before.
2. It calls `ConsecutiveLossCounter.RecordClosedTrade(position.NetProfit)`.
3. It prints the resulting `Count`.

## 7. Architecture Boundary

- Domain/policy: `Biz/ConsecutiveLossCounter` — pure, no cAlgo dependency, unit tested.
- Application: `Orders/PdhpdlOrderExecutor` owns the single instance and feeds it.

Only closes that pass `IsStrategyPosition` reach the counter, so manual trades and other bots on
the same symbol cannot pollute the streak.

## 8. Dependencies

`ConsecutiveLossCounter` depends on nothing outside the language runtime.

## 9. External Details

None. The counter needs no clock, no bar data, and no broker state — only a number handed to it
by the executor.

## 10. Test Strategy

`tests/Pdhpdl.Tests/Biz/ConsecutiveLossCounterTests.cs` covers: fresh counter is zero, losses
accumulate, a profit resets, breakeven resets, and a loss after a reset starts over at one.

## 11. Risks and Trade-offs

- The count lives in memory, so restarting the cBot clears it. Acceptable while the count is
  observational only; a persisted count would matter once a rule acts on it.
- The counter is created inside the executor rather than injected from the composition root. It
  has no configuration and no collaborators, and it is unit tested on its own, so injecting it
  would only add wiring. Injection becomes worthwhile the day a rule needs to read the count
  from somewhere else.
