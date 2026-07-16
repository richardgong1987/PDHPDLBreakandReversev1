# Batch backtest script (run_conditions.py)

Runs cTrader historical backtests (`backtest`) one row at a time, following the plan in
`conditions.numbers`. Each run finishes and automatically moves on to the next, and each
writes its trade details to its own CSV.

## Installing dependencies

```bash
pip install -r scripts/requirements.txt
```

Third-party dependencies: `numbers-parser` (reads `.numbers` spreadsheets), plus `pandas` +
`matplotlib` (summarize backtest reports and draw the bar chart); everything else is the
Python standard library.

> The package is named `numbers-parser` (hyphen); the code does `import numbers_parser`
> (underscore) — same package.

## Environment config (.env)

Account, paths, credentials and other environment-specific settings live in a `.env` file
instead of being hard-coded, so you don't have to copy the script per environment — just
maintain the env file:

```bash
cp scripts/.env.example scripts/.env        # first time: create the dev config and fill it in
```

Required keys: `AUTH_TOKEN`, `CTRADER_BIN`, `ALGO_PATH`, `CTID`, `ACCOUNT`;
optional: `DATA_MODE` (default `m1`), `BALANCE` (default `10000`).

`.env` and `.env-prod` contain the auth token and are ignored in `.gitignore`, so they are
never committed; only the `.env.example` template is version-controlled.

## How to use

1. Open `backtester/conditions.numbers` in Numbers, add/remove rows and edit parameters as needed.
2. Run:

   ```bash
   python3 scripts/run_conditions.py                          # reads scripts/.env by default
   python3 scripts/run_conditions.py --env-file scripts/.env-prod   # use the production config
   python3 scripts/run_conditions.py --jobs 4                 # run up to 4 at a time
   python3 scripts/run_conditions.py --jobs 1                 # run one at a time (sequential)
   ```

3. Each run's result CSV is written to `~/Documents/`.

Before running, the script validates inputs (missing env file / missing required keys,
missing plan columns, invalid date format) and fails with a clear message, so it never runs
with a broken config.

### Parallel backtests (--jobs)

`--jobs N` controls how many backtests run at once. **The default is half the CPU cores**
(leaving headroom for the system); `--jobs 1` restores sequential execution. Backtesting is
CPU/memory intensive — going beyond the physical core count usually isn't faster and just
makes the runs contend for resources.

The first time you run a given symbol in parallel, it's best to run one with `--jobs 1` first
to warm the m1 data cache, then scale up — this avoids multiple processes downloading the
same data at once and conflicting. The summary chart `final_report.png` is generated once
after all tasks finish, so concurrency doesn't affect it.

> cTrader's official docs state the backtesting engine supports running multiple backtest
> processes in parallel, but concurrency is not explicitly endorsed at the CLI level. For a
> first parallel run, validate a small sample (2–3 rows) produces correct reports/CSVs before
> scaling up.

## Plan columns (conditions.numbers)

Columns are matched **by name**, so their order doesn't matter; the header row must contain
these column names (the names are Chinese literals that the code matches exactly):

| Column (literal) | Meaning | Mapped parameter | Notes |
|---|---|---|---|
| `种类` | Symbol | `--symbol` | e.g. `XAUUSD`, `EURUSD` |
| `周期` | Timeframe | `--period` | e.g. `m5`, `m15`, `h1`, `H4` |
| `回撤开仓模式` | Entry mode | `--EntryModel` | see mapping below |
| `止盈目标` | Take-profit multiple (R) | `--TakeProfitR` | `2.0` becomes `2`; `1.75` kept as is |
| `起始日期` | Backtest start | `--start` | **DD/MM/YYYY** (day/month/year, UTC) |
| `结束日期` | Backtest end | `--end` | **DD/MM/YYYY** |
| `最大浮盈` | — | not used yet | read but ignored |

**Entry mode → EntryModel mapping:**

| Name | Value |
|---|---|
| Close | 0 |
| Pb25 | 1 |
| Pb382 | 2 |
| Pb50 | 3 |

Blank rows and rows missing required fields are skipped automatically.

## Output CSV filename

Built by joining the plan fields (dates use the compact `YYYYMMDD` form):

```
<symbol>-<period>-<entry-mode>-<EntryModel value>-<take-profit>-<start-date>-<end-date>.csv
```

Example: `XAUUSD-h1-Close-0-2-20260601-20260630.csv`. Files are written to `~/Documents/`
(the path is decided by the cBot's own logger).

Each backtest also produces a **backtest report JSON** (`--report-json`), in the **same
directory with the same name** as the CSV, only with the extension changed to `.json`:

```
~/Documents/XAUUSD-h1-Close-0-2-20260601-20260630.json
```

This way each run's CSV (trade details) and report.json (backtest statistics) sit together
as a matched pair with one-to-one filenames.

## Summary outputs (final_report.png + final_summary_report.csv)

Once **all** tasks finish, the script scans **all** backtest report JSONs in the output
directory, summarizes them with `pandas`, and writes two files to `~/Documents/`:

**1. `final_report.png`** — two stacked bar charts drawn with `matplotlib`:

- **Top chart**: net profit per report (`main.netProfit`), green for profit, red for loss.
- **Bottom chart**: win rate per report (`winningTrades.all / totalTrades.all`).
- The X-axis label is each report's full filename (without extension), so you can tell at a
  glance which parameter set / date range it is.

**2. `final_summary_report.csv`** — one row per report, columns:

```
文件名, 起始日期, 结束日期, 周期, 止盈目标, 胜率%, 盈利金额
XAUUSD-m5-Close-0-2-20240101-20240131, 20240101, 20240131, m5, 2R, 29%, -509$
```

Only `胜率%` (win rate) and `盈利金额` (net profit) come from the report JSON; the rest
(filename, start/end dates, period, take-profit) are parsed straight from the report filename.
Written with a UTF-8 BOM so the Chinese headers open correctly in Excel.

Both are generated once at the end of the batch. The summary logic lives in the `summary/`
package; `report_summary.py` is a thin CLI over it that can be run standalone to (re)generate
both files manually at any time — e.g. mid-run in another terminal, or without re-running
backtests:

```bash
python3 scripts/report_summary.py                 # scans ~/Documents by default
python3 scripts/report_summary.py --dir <dir>     # specify the report directory
```

> Note: the summary covers **all** report JSONs in the directory, including leftovers from
> previous runs. To summarize only one batch, clear the old `*.json` from the directory first.

## Tunables

Environment-related (edit in `.env` / `.env-prod`):

- `AUTH_TOKEN` / `CTRADER_BIN` / `ALGO_PATH` / `CTID` / `ACCOUNT` (account, paths, credentials)
- `BALANCE` (starting capital, default `10000`)
- `DATA_MODE` (backtest data mode, default `m1`; options `open`, `m1-csv`)

Strategy parameters (edit in `CBOT_FIXED_PARAMS` in `backtest/command.py`):

Each key must match the cBot's C# property name exactly (the cTrader CLI matches by property
name, not by the Chinese display name). The current fixed set maps one-to-one to cBot
parameters:

| CBOT_FIXED_PARAMS | Default | Meaning |
|---|---|---|
| `Strategy` | `0` | Strategy mode (enum int: AB=0, A=1, B=2) |
| `ResetTradeLogOnStart` | `True` | Clear the trade-log CSV on start |
| `RiskPct` | `1` | Risk percent per trade |
| `RiskSafetyFactor` | `1` | Risk safety factor |
| `StopOffsetTicks` | `15` | Stop-loss offset in ticks |
| `MinStopLossPips` | `5` | Minimum stop-loss (pips) |
| `SaturdayForceCloseHour` | `5` | Saturday force-close hour (Japan time) |
| `SaturdayForceCloseMinute` | `30` | Saturday force-close minute |
| `ShowMovingAverages` | `True` | Whether to draw the moving averages (RMA 1 + RMA 2) |
| `MaSource` | `0` | MA source (enum int: HigherTimeFrame=0, ChartTimeFrame=1) |
| `MaFastPeriod` | `13` | MA period RMA 1 (fast) |
| `MaSlowPeriod` | `55` | MA period RMA 2 (slow) |
| `MaTimeFrameMinutes` | `120` | MA timeframe (minutes) |
| `ShowDebugLogs` | `False` | Show debug logs |
| `IsDebug` | `False` | Debug mode |

> The MA parameters aren't just cosmetic: `MaSource`/`MaFastPeriod`/`MaSlowPeriod`/
> `MaTimeFrameMinutes` feed the RMA series, and the long/short signal filters direction using
> the relative position of the fast/slow RMA — so they directly affect the trades a backtest
> produces. Enums (`Strategy`, `MaSource`) are passed as integer values, matching how
> `EntryModel` is handled.

cBot parameters not listed here (e.g. `NewsBlackoutWindows`) are not passed and fall back to
the cBot's own defaults; add them to `CBOT_FIXED_PARAMS` if you need to pin them. `EntryModel`,
`TakeProfitR`, and `FileName` are overridden per plan row and are not kept here.

## Code structure

The CLI entry point `run_conditions.py` only does the "composition" (parse args + wire the
modules together); the actual logic is split by responsibility into the `backtest/` package,
each part doing one thing and decoupled from the others:

```
run_conditions.py     Backtest CLI entry point (composition root: parse_args + main)
report_summary.py     Chart CLI entry point (refresh final_report.png standalone)
backtest/             Running backtests
  config.py           read .env, produce Config (account/paths/credentials/capital/data mode)
  plan.py             read conditions.numbers, produce backtest tasks (ConditionRow)
  command.py          task + config -> cTrader CLI command (includes CBOT_FIXED_PARAMS)
  runner.py           run tasks sequentially/in parallel; generate the chart once at the end
summary/              Summarizing results
  metrics.py          read report JSONs -> DataFrame (win rate / net profit)   [data]
  naming.py           parse a report filename -> its fields (symbol/period/…)  [data]
  chart.py            DataFrame -> two-panel bar chart PNG                       [presentation]
  table.py            DataFrame -> final_summary_report.csv                      [presentation]
  report.py           scan dir -> summarize -> chart + csv (public: update_final_report)
```

Dependency direction: `run_conditions → backtest.runner → {backtest.command, summary}`, and
`report_summary → summary`. Within `summary`: `report → {metrics, chart, table}` and
`table → naming`. Leaf modules (`config` / `plan` / `command` / `metrics` / `naming` /
`chart` / `table`) don't depend back on their orchestrators.

## Key design notes

- Uses the CLI's **`backtest`** subcommand (historical backtest, stops when done), **not
  `run`** (`run` is live/forward execution, stays connected to the live account and never
  exits on its own).
- The command includes **`--exit-on-stop`**: after a backtest finishes the process would not
  exit on its own (it idles); this flag makes it terminate so the script can move to the next.
- The command includes **`--full-access`**: allows the cBot to write out its own trade CSV.
- Dates are always passed as **DD/MM/YYYY (UTC)** to cTrader; the script validates the format.

Official CLI docs: <https://help.ctrader.com/ctrader-algo/documentation/ctrader-cli/>
