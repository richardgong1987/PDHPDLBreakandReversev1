# MaxKeylevelTimes Design

## 1. Business Purpose

When the same key level (PDH or PDL) keeps producing entries back to back, that level is
usually already exhausted and further trades tend to accumulate losses. This feature caps how
many orders may be placed **consecutively** on the same key level.

## 2. Use Case

The strategy rejects a signal when its key level has already been traded `MaxKeylevelTimes`
times in a row without any order on the other key level in between.

## 3. Input Model

- `MaxKeylevelTimes` (cBot parameter, default `0` = unlimited)
- signal key level (`PdhpdlSignalModel.KeyLevel`) — `"PDH"` or `"PDL"`

## 4. Output Model

`PdhpdlSignalModel.IsLongSignal` / `IsShortSignal` become `false` once the streak is used up.

## 5. Domain Rules

- The counter tracks a **streak**, not a daily total. It has no day boundary.
- An order on the other key level restarts the streak at 1, which immediately unblocks the
  previously blocked level.
- Only orders that were actually submitted successfully are counted. Signals blocked by the
  risk guard, by an existing position, or by an invalid plan do not consume the streak.
- `MaxKeylevelTimes <= 0` disables the filter (existing behaviour is preserved).

Example with `MaxKeylevelTimes = 3`:

```text
PDH, PDH, PDH  -> the next PDH signal is rejected, a PDL signal is still allowed
PDH, PDH, PDL  -> the streak is now PDL=1; PDH starts over from zero
```

## 6. Application Flow

1. `MainBiz.Evaluate` resolves the signal and its key level as before.
2. The resolved signal is kept only if `ConsecutiveKeyLevelOrderLimit.HasReachedConsecutiveLimit`
   is false.
3. `PdhpdlOrderExecutor.ExecuteIfSignal` calls `RecordPlacedOrder` after a successful submit.

## 7. Architecture Boundary

- Domain/policy: `Biz/ConsecutiveKeyLevelOrderLimit` — pure, no cAlgo dependency, unit tested.
- Application: `Biz/MainBiz` applies the policy; `Orders/PdhpdlOrderExecutor` feeds it.
- Composition root: the robot shell constructs the limit from the parameter and shares the
  single instance between the detector and the executor.

## 8. Dependencies

`ConsecutiveKeyLevelOrderLimit` depends on nothing outside the language runtime.

## 9. External Details

None. The rule needs no clock, no bar data, and no broker state.

## 10. Test Strategy

`tests/Pdhpdl.Tests/Biz/ConsecutiveKeyLevelOrderLimitTests.cs` covers: unlimited mode, blocking
the (N+1)-th consecutive order, the other key level staying open, streak restart after an order
on the other level, and empty key level.

## 11. Risks and Trade-offs

- The streak lives in memory, so restarting the cBot clears it.
- The streak never expires on its own: if only PDH ever triggers, PDH stays blocked until a PDL
  order is placed. Adding a daily reset would be a separate, explicit rule.
