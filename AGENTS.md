# AGENTS.md

## Project

This is a cTrader cBot project named:

`PDHPDLBreakandReversev1`

The strategy is a cTrader/cAlgo C# implementation of the TradingView Pine Script strategy:

`PDH/PDL Break & Reverse v1`

The main idea is:

* Read previous day high and previous day low.
* Draw PDH / PDL step lines on the chart.
* Detect false breakouts:

  * `high > PDH && close < PDH` means short signal.
  * `low < PDL && close > PDL` means long signal.
* Draw visual signal markers:

  * Long signal: green up triangle + `L`.
  * Short signal: red down triangle + `S`.
* Execute orders:

  * TP1 leg: 50% volume, TP at 2R.
  * Runner leg: 50% volume, TP at 4R.
* Write each successful trade entry into a CSV file.

## Important user preferences

* Do not put Chinese comments in code.
* Keep the main Robot class clean.
* Put separate responsibilities into separate classes.
* Avoid exposing visual-only parameters to users.
* Only expose real strategy parameters.
* Prefer simple, maintainable C# over over-engineered abstractions.
* Do not mix drawing logic, signal detection, order execution, and CSV writing in the same class.

## Current architecture

### `PDHPDLBreakandReversev1`

Main cBot entry point.

Responsibilities:

* cBot lifecycle: `OnStart`, `OnBar`, `OnTick`, `OnStop`.
* Initialize services.
* Coordinate:

  * Draw PDH/PDL lines.
  * Detect signal.
  * Draw signal marker.
  * Execute order.
  * Write CSV through order executor.

Current Robot attribute:

```csharp
[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true)]
```

`FullAccess` is required because the cBot writes a CSV file to macOS Documents.

Current exposed parameters:

```csharp
[Parameter("Line Thickness", DefaultValue = 3)]
public int LineThickness { get; set; }

[Parameter("Show Debug Logs", DefaultValue = false)]
public bool ShowDebugLogs { get; set; }

[Parameter("Risk % per Trade", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1)]
public double RiskPct { get; set; }

[Parameter("Stop Offset Ticks", DefaultValue = 15, MinValue = 0, MaxValue = 1000)]
public int StopOffsetTicks { get; set; }

[Parameter("TP1 R", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
public double Tp1R { get; set; }

[Parameter("TP2 R", DefaultValue = 4.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
public double Tp2R { get; set; }
```

## Existing classes

### `PdhpdlLines`

Draws PDH / PDL step lines.

Responsibilities:

* Read daily bars using `MarketData.GetBars(TimeFrame.Daily, symbolName)`.
* For each day, draw:

  * Previous day high as red step line.
  * Previous day low as lime step line.
* Uses `Chart.DrawTrendLine`.
* Keeps chart object names and can clear them.

Important logic:

```csharp
double pdh = _dailyBars.HighPrices[i - 1];
double pdl = _dailyBars.LowPrices[i - 1];
```

This corresponds to Pine Script:

```pine
high[1]
low[1]
```

### `PdhpdlUtils`

Pure utility class.

Responsibilities:

* Calculate how many days to draw.
* Read previous day levels.
* Detect false breakouts.
* Generate `PdhpdlSignal`.

False breakout logic:

```csharp
public static bool IsFalseBreakUp(double high, double close, double pdh)
{
    return high > pdh && close < pdh;
}

public static bool IsFalseBreakDown(double low, double close, double pdl)
{
    return low < pdl && close > pdl;
}
```

Important cTrader rule:

* `OnBar()` fires when a new bar starts.
* Therefore, the last fully closed bar is `Bars.Count - 2`.

### `PdhpdlSignal`

Data object for signal detection result.

Fields include:

* `HasData`
* `BarIndex`
* `BarTime`
* `High`
* `Low`
* `Close`
* `Pdh`
* `Pdl`
* `IsLongSignal`
* `IsShortSignal`

### Current Harami breakout rule

The Harami signal is a strategy-specific inside-bar breakout, not the classic
reverse-the-parent-body pattern.

For candles ordered as `current [0]`, `previous [1]`, `earlier [2]`:

* `earlier [2]` must strictly contain `previous [1]` by wick range and body range.
* `current.Close > previous.High` means `Buy` (`孕线上破`).
* `current.Close < previous.Low` means `Sell` (`孕线下破`).
* A close inside the previous candle's range means `None`.
* Parent and inside-bar body direction must not affect the result.
* `HaramiSingle` is used for entries. `HaramiDouble` currently mirrors it only for legacy compatibility and is not used by the order detector.

Do not change this back to the obsolete rule where `previous [1]` contains `current [0]`
and the signal reverses the parent candle's body direction. Keep the C# implementation,
unit tests, `docs/design/hanjin-signals-26.md`, and the Pine reference library aligned.

### `PdhpdlSignalMarkers`

Draws visual signal markers.

Responsibilities:

* Draw long marker:

  * `ChartIconType.UpTriangle`
  * text `L`
  * color `Color.Lime`
* Draw short marker:

  * `ChartIconType.DownTriangle`
  * text `S`
  * color `Color.Red`

Visual offsets are intentionally hardcoded because they are not strategy parameters.

Current constants:

```csharp
private const int IconOffsetTicks = 120;
private const int TextOffsetTicks = 360;
private const int TextFontSize = 18;
```

Do not expose these as `[Parameter]`.

### `RiskUtil`

Pure position-sizing utility.

It already exists and should remain independent from cAlgo runtime as much as possible.

Responsibilities:

* `FloorToStep`
* `Clamp`
* `CalcRiskMoney`
* `CalcLossPerLot`
* `NormalizeVolume`
* `CalcVolumeByRisk`

Important warning:

The current order sizing may be wrong or too large for XAUUSD. In logs, it generated very large lots, causing account equity to drop to zero during backtest.

Need to verify unit assumptions carefully:

* cTrader `ExecuteMarketOrder` expects volume in units.
* `QuantityToVolumeInUnits` converts lots to volume units.
* XAUUSD lot/unit/tick-value behavior may not match simple assumptions.

### `PdhpdlOrderPlan`

Data object for order plan.

Fields include:

* `IsValid`
* `RejectReason`
* `TradeType`
* `EntryPrice`
* `StopPrice`
* `Tp1Price`
* `Tp2Price`
* `RiskPrice`
* `StopLossPips`
* `Tp1Pips`
* `Tp2Pips`
* `TotalLots`
* `TotalVolumeInUnits`
* `VolumePerLegInUnits`
* `Tp1Label`
* `RunnerLabel`

### `PdhpdlOrderExecutor`

Converts a valid `PdhpdlSignal` into actual cTrader market orders.

Current behavior:

* Skip if no signal.
* Skip if a strategy position already exists.
* Create an order plan.
* Execute two market orders:

  * TP1 leg
  * Runner leg
* After both orders succeed, write CSV record.

Current label prefix:

```csharp
private const string LabelPrefix = "PDHPDL_V1";
```

Current comments:

```csharp
private const string Tp1Comment = "TP1";
private const string RunnerComment = "RUNNER";
```

Important warning:

There is currently a possible bug:

* `_timeFrame` exists but may not be assigned unless the constructor receives `timeFrame`.
* Fix by passing `Bars.TimeFrame.ToString()` from Robot and assigning `_timeFrame = timeFrame`.

### `PdhpdlTradeCsvLogger`

Writes trade records to CSV.

Current path:

```text
/Users/hanjingong/Documents/pdhpdl-trades.csv
```

This was confirmed in cTrader logs:

```text
*****CSV trade record added. Path: /Users/hanjingong/Documents/pdhpdl-trades.csv
```

Responsibilities:

* Create CSV if missing.
* Write header.
* Append one row per successful trade entry.
* Use UTF-8 BOM if possible so Excel opens Chinese headers correctly.

CSV headers:

```text
编号, 多空, 关键位, 信号, 收线入场, 回撤25入场, 回撤38.2入场, 回撤50入场, 备注, Symbol, TimeFrame, EntryTime, EntryPrice, StopPrice, TP1Price, TP2Price, RiskPrice, VolumeInUnits
```

### `PdhpdlTradeCsvRecord`

Data object for one CSV row.

Fields include:

* `Side`
* `KeyLevel`
* `Signal`
* `CloseEntryResult`
* `Pullback25Result`
* `Pullback382Result`
* `Pullback50Result`
* `Comment`
* `Symbol`
* `TimeFrame`
* `EntryTime`
* `EntryPrice`
* `StopPrice`
* `Tp1Price`
* `Tp2Price`
* `RiskPrice`
* `VolumeInUnits`

## Current known issues

### 1. Position sizing is too large

Backtest logs showed examples like:

```text
Lots: 81.56
VolumePerLegUnits: 4078
```

On chart this appeared as around:

```text
40.78 Lots
```

This caused:

```text
account equity have dropped to 0
```

Do not add new features until position sizing is fixed.

Expected behavior:

* If starting capital is 100000 and Risk % is 1, the maximum total risk for both legs combined should be around 1000 account currency.
* Since TP1 and Runner split volume 50/50, each leg should risk around 500.
* If stop loss is hit, total loss should be close to 1% of equity, not 100%+.

### 2. Backtesting drawings disappear when cBot stops

This is expected because `OnStop()` currently clears drawings:

```csharp
_signalMarkers?.Clear();
_pdhpdlLines?.Clear();
```

During development, it may be better to not clear drawings on stop so the user can inspect the chart.

### 3. TimeFrame may be missing in CSV

Fix by passing time frame to `PdhpdlOrderExecutor`.

Robot should call:

```csharp
_orderExecutor = new PdhpdlOrderExecutor(
    this,
    Symbol,
    SymbolName,
    Bars.TimeFrame.ToString(),
    RiskPct,
    StopOffsetTicks,
    Tp1R,
    Tp2R,
    _csvLogger
);
```

Executor constructor should assign:

```csharp
_timeFrame = timeFrame;
```

## Next priority

Fix position sizing.

Suggested safer approach:

1. Temporarily disable real order execution.
2. Only print `PdhpdlOrderPlan`.
3. Log:

  * Account equity
  * RiskPct
  * Expected risk money
  * Entry
  * Stop
  * RiskPrice
  * StopLossPips
  * TickSize
  * TickValue
  * PipSize
  * LotSize
  * VolumeInUnitsMin
  * VolumeInUnitsMax
  * VolumeInUnitsStep
  * TotalLots
  * TotalVolumeInUnits
  * VolumePerLegInUnits
  * Estimated loss if stop is hit
4. Confirm estimated loss is close to `equity * riskPct / 100`.

Possible fix:

Use cTrader native volume calculation:

```csharp
double totalVolumeInUnits = _symbol.VolumeForProportionalRisk(
    ProportionalAmountType.Equity,
    _riskPct,
    stopLossPips,
    RoundingMode.Down
);
```

Then split:

```csharp
double volumePerLegInUnits = _symbol.NormalizeVolumeInUnits(
    totalVolumeInUnits / 2.0,
    RoundingMode.Down
);
```

Do not use `QuantityToVolumeInUnits` unless the value is definitely lots.

## Development commands / workflow

This is a cTrader cBot project. Build is normally done inside cTrader Algo UI.

When editing by Codex CLI:

* Modify `.cs` files in the cAlgo source folder.
* Keep one class per file where possible.
* Do not introduce external NuGet dependencies.
* Do not add Chinese comments in code.
* After editing, build from cTrader UI.
* Use Logs tab to validate runtime behavior.

Suggested project directory:

```bash
cd "/Users/hanjingong/cAlgo/Sources/Robots/PDHPDLBreakandReversev1"
```

Start Codex CLI from the project directory:

```bash
codex
```

First prompt to Codex:

```text
Read AGENTS.md and summarize the current project state. Do not modify files yet.
```

Then give a specific task:

```text
Fix the position sizing bug in PdhpdlOrderExecutor. First disable real order execution and add detailed risk diagnostics to logs. Do not add new features.
```

## Current completed milestones

* PDH / PDL step lines drawn correctly.
* False breakout detection works.
* Signal markers show on chart.
* Order executor can trigger orders.
* CSV file creation and append work.
* CSV path confirmed:
  `/Users/hanjingong/Documents/pdhpdl-trades.csv`

## Current main risk

The strategy currently opens oversized positions.

Fix volume calculation before adding:

* Breakeven logic
* Pullback entries
* Advanced CSV result updates
* Optimisation features
* Extra filters

## Coding style

Use clear C#.

This project uses Java-style C# formatting. Put opening braces on the same line as the
declaration or control statement:

```csharp
public void ExecuteIfSignal(PdhpdlSignal signal) {
    if (signal == null || !signal.HasData)
        return;
}
```

Do not reformat touched code back to C# default Allman-style braces.

Prefer:

```csharp
if (signal == null || !signal.HasData)
    return;
```

Avoid:

```csharp
// Chinese comments
```

Use English comments only when necessary.

Keep methods small, but do not over-abstract.

Prefer responsibility-based class names:

* `PdhpdlLines`
* `PdhpdlUtils`
* `PdhpdlSignal`
* `PdhpdlSignalMarkers`
* `PdhpdlOrderPlan`
* `PdhpdlOrderExecutor`
* `PdhpdlTradeCsvLogger`
* `PdhpdlTradeCsvRecord`

Avoid names like:

* `OrderUtil` for drawing markers
* `Helper` for everything
* `Manager` unless the responsibility is very clear

## Notes about cTrader behavior

### Parameters

`[Parameter(DefaultValue = ...)]` only affects new cBot instances.

Existing instances keep their own saved parameter values.

If a default parameter value is changed in code, delete and recreate the cBot instance to see the new default value.

### Multiple instances

A single cBot class can have multiple running instances.

Each symbol/timeframe instance has its own state and its own `OnStart()`.

If two instances are started, `OnStart()` runs twice.

### OnBar

`OnBar()` runs when a new bar opens.

Therefore, the fully closed bar is:

```csharp
int closedBarIndex = Bars.Count - 2;
```

Do not use `Bars.Count - 1` for confirmed signal logic.

### Drawings

cTrader chart objects may be cleared when the cBot stops if `OnStop()` removes them.

During backtest debugging, consider not clearing drawings in `OnStop()`.

### File access

Writing CSV requires:

```csharp
[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true)]
```

If cTrader asks about synchronization conflict because of full access rights, keep the local version if it contains the latest source code.

## CSV design intent

The CSV is intended to support manual trade study.

It is not only for raw execution logs.

The image-based manual table included fields like:

```text
编号
多空
关键位
信号
收线入场
回撤25入场
回撤38.2入场
回撤50入场
备注
```

Current implementation adds engineering fields:

```text
Symbol
TimeFrame
EntryTime
EntryPrice
StopPrice
TP1Price
TP2Price
RiskPrice
VolumeInUnits
```

Future possible CSV improvements:

* Add `TradeId`
* Add `PositionId`
* Add `TP1PositionId`
* Add `RunnerPositionId`
* Add final result after trade closes
* Update `收线入场` from blank to `2R`, `0R`, `-1R`, or `未成交`
* Track pullback entry simulations separately
* Track signal category:

  * 顶分型
  * 底分型
  * 吞没
  * pinbar
  * 孕线上破
  * 孕线下破

Do not implement these until position sizing is fixed.

## Immediate task for Codex

The next task should be:

```text
Fix the oversized position sizing in PdhpdlOrderExecutor.

Requirements:
1. Temporarily disable actual ExecuteMarketOrder calls.
2. Print a detailed risk diagnostic plan instead.
3. Show expected risk money and estimated loss at stop.
4. Ensure estimated loss is close to Account.Equity * RiskPct / 100.
5. Do not add new strategy features.
6. Do not modify drawing classes.
7. Do not modify CSV classes unless necessary.
```
