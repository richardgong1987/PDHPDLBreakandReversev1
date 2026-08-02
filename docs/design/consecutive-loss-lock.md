# Consecutive Loss Lock Design

Supersedes `consecutive-loss-counter.md`. The counter was observational only; it is now the
state of a rule that actually blocks entries.

## 1. Business Purpose

A run of losses usually means the current regime does not suit the strategy. Rather than keep
paying for the same setup, stop trading until the market visibly wakes up — a candle much wider
than the one we were trading on when we started losing.

## 2. Use Case

After `Nlock` consecutive losing trades the bot stops opening positions, and resumes once price
has travelled `MaxBarRangeAtr × frozen ATR` away from where that losing trade was entered.

## 3. Input Model

- `Nlock` — consecutive losses that arm the lock (`<= 0` disables it).
- `MaxBarRangeAtr` — ATR multiple of price travel required to release it.
- Per closed trade: `Position.NetProfit`, `Position.EntryPrice`, and the ATR14 of the bar that
  trade was **entered** on.
- Per closed bar: its `High` and `Low`.

## 4. Output Model

`ConsecutiveLossLock.IsLocked`. `PdhpdlOrderExecutor` refuses to open anything while it is true.
`ConsecutiveLosses`, `LockedAtr`, `AnchorPrice` and `RequiredDistance` exist for logging.

## 5. Domain Rules

- The streak is global: every closed strategy position counts, regardless of key level or
  direction. `NetProfit < 0` extends it; profit or exact breakeven resets it to zero and
  unlocks. That matches the `盈利 / 亏损` split the trade CSV writes.
- The lock arms the moment the streak reaches `Nlock`, freezing both the ATR and the **entry
  price** of the most recent losing trade. Re-arming later overwrites both, so the reference
  always tracks the most recent loss.
- **Release measures how far price travelled, not how tall one candle is.** A move is usually
  built by many small bars; requiring a single bar wider than `3 × ATR` almost never fires. See
  the worked example below.
- Direction is irrelevant: moving far enough either way counts as having left the regime.
- **The frozen ATR is used, never the live ATR.** When volatility drops the live ATR drops with
  it, lowering the bar and effectively self-releasing — which defeats the purpose.
- Release is evaluated on **every closed bar**, not only on bars that produce a signal, and it
  is one-way: once any bar reaches the distance the lock is gone, and price coming back does not
  restore it. Only a fresh streak re-arms it.
- The threshold is strict: a bar exactly at `MaxBarRangeAtr × LockedAtr` away does not release.
- If the frozen ATR is missing (`<= 0`), the next bar releases the lock. Letting one extra trade
  through beats locking the bot out indefinitely.

### Worked example (why single-bar range was wrong)

`S_Pin_2`, XAUUSD m5, 2026-06-11 09:45, entry `4063.52`, stopped out 09:52. `Nlock = 1`, so the
lock armed with frozen ATR `11.6129` → threshold `34.84`.

Over the next 32 bars price climbed to `4118.04` — **54.52 away from the anchor, 4.7× the ATR**.
But the widest single bar in that stretch was only `17.04`, and **no bar at all** exceeded
`34.84` for the rest of that day. Under the old rule the bot stayed locked through the entire
move. Under the distance rule it releases at 10:15 (`High 4099.35`, 35.83 away), in time for the
fractal-top signal that followed.

`ConsecutiveLossLockTests.unlocks_on_the_real_s_pin_2_hill` pins this case with the real numbers.

## 6. Application Flow

Every closed bar, in `HandleClosedBarSignal`:

1. The shell fills `signalModel.Atr` with the ATR14 of that bar.
2. `lossLock.RecordClosedBar(high, low)` — may release the lock.
3. `orderExecutor.ExecuteIfSignal(signalModel)` — refuses while `IsLocked`.

So a bar that both releases the lock and carries a signal trades on that same bar.

On close, in `PdhpdlOrderExecutor.OnPositionClosed`:

4. Look up the ATR remembered when that position was entered.
5. `lossLock.RecordClosedTrade(netProfit, atrAtEntry)` — may arm the lock.

## 7. Architecture Boundary

- Domain/policy: `Biz/ConsecutiveLossLock` — pure, no cAlgo dependency, unit tested.
- Application: `Orders/PdhpdlOrderExecutor` feeds closed trades and gates on `IsLocked`; it
  carries `_positionEntryAtr` / `_pendingEntryAtrByLabel` so a close can recover its entry ATR,
  mirroring the existing entry-equity bookkeeping.
- Composition root: the robot shell constructs the lock from the two parameters and drives
  `RecordClosedBar` each bar.

Only closes that pass `IsStrategyPosition` reach the lock, so manual trades and other bots on
the same symbol cannot arm it.

## 8. Dependencies

`ConsecutiveLossLock` depends on nothing outside the language runtime.

## 9. External Details

None. ATR values and bar geometry are handed to it as plain numbers.

## 10. Test Strategy

`tests/Pdhpdl.Tests/Biz/ConsecutiveLossLockTests.cs` covers: starts unlocked, no lock below the
streak, locking at the streak, small bar / exact-threshold bar do not release, a wide bar does,
release is sticky across later small bars, a win and a breakeven clear the streak, re-arming
picks up the newest entry ATR, `Nlock = 0` disables the rule, and a missing frozen ATR releases
on the next bar.

## 11. Risks and Trade-offs

- All state is in memory, so restarting the cBot clears both the streak and the lock. Fine for
  backtests (one process per run); a live restart silently drops the lock.
- Release depends only on a single bar's range — it ignores direction and does not accumulate,
  so a lone volatility spike releases the lock even if the regime has not really changed. That
  is the rule as specified; a "price travelled N×" variant would need different state.
- `IsBigK` still uses its own hard-coded `3 ×` ATR multiple, separate from `MaxBarRangeAtr`.
  Two thresholds now describe "a big bar"; they were deliberately left independent.
