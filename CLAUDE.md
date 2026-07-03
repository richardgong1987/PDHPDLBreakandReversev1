# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **cTrader cBot** (automated trading robot) written in C# against the cAlgo API, targeting
`net6.0`. The strategy is **PDH/PDL Break and Reverse**: it detects false breakouts of the
Previous Day High/Low, sizes an order against a fixed per-trade risk budget, places it, and
logs each trade to CSV. The strategy is implemented; `PDHPDL Break and Reverse v1.cs` is the
Robot lifecycle shell that wires the pieces together (the composition root).

## Module map

Behavior classes live beside the feature they serve; all data types live in `Models/`
(suffixed `Model`):

- `Signals/` — `PdhpdlSignalRules` (pure long/short predicates, unit tested),
  `PdhpdlSignalDetector` (reads the closed bar + previous-day levels), `PdhpdlSignal` (data).
- `Orders/` — `PdhpdlOrderPlanner` (pure sizing/geometry, unit tested) talks to the broker
  only through the `IPdhpdlSymbolModel` port; `PdhpdlOrderExecutor` gates on risk/exposure,
  submits orders, and tracks CSV row ids.
- `Risk/` — `PdhpdlRiskGuard` (time/news/weekend windows + risk-money, pure, unit tested),
  `RiskUtil`.
- `LineDrawer/` — `PdhpdlLines`, `PdhpdlSignalMarkers` (chart drawing).
- `OrderLogger/` — `PdhpdlTradeCsvLogger`.
- `Models/` — data types: `PdhpdlOrderPlanModel`, `PdhpdlTradeDirectionModel`,
  `PdhpdlEntryModel`, `PdhpdlRiskGuardConfigModel`, `NewsBlackoutWindowModel`,
  `PdhpdlTradeCsvRecordModel`, the `IPdhpdlSymbolModel` port, and its `CAlgoSymbolModel`
  adapter (the one Models/ file that references `cAlgo.API`).

Rule of thumb: classes with no `using cAlgo.API` are pure and testable; keep them that way.
`CAlgoSymbolModel` is the sole broker adapter — it is the only Models/ file that touches
cAlgo, and it is never linked into the test project. The design rationale lives in
`docs/design/refactor-structure.md`.

## Build & run

```bash
# Build (from repo root)
dotnet build "PDHPDL Break and Reverse v1.sln"          # Debug
dotnet build "PDHPDL Break and Reverse v1.sln" -c Release
```

A successful build produces a `.algo` package under
`PDHPDL Break and Reverse v1/bin/<Config>/net6.0/`. The `.algo` file is the deployable
cBot artifact loaded by the cTrader desktop platform.

The cBot itself is validated by running it in cTrader's backtester/optimizer, not via a CLI
runner. Iteration loop: edit `.cs` → `dotnet build` → load/refresh the `.algo` in cTrader →
backtest.

Pure (framework-independent) helpers are unit-tested with xUnit under `tests/`:

```bash
./scripts/test.sh                                       # build cBot + run all tests
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj"      # tests only
```

The test project is intentionally **not** part of the `.sln` (which cTrader builds) and
targets `net10.0` rather than the cBot's `net6.0` — it links pure source files directly (via
`<Compile Include>`) instead of referencing the cBot project, so tests never pull in the
`cTrader.Automate` / cAlgo.API dependency. Keep new domain/risk logic pure so it can be
tested this way.

The `cTrader.Automate` NuGet package (versioned `*`) supplies the `cAlgo.API.*` assemblies;
restore happens automatically on build.

## Code structure

A cBot is a single class deriving from `cAlgo.API.Robot` in namespace `cAlgo.Robots`,
annotated with `[Robot(...)]`. The framework drives it through lifecycle overrides — there is
no `Main`:

- `OnStart()` — one-time setup (read parameters, attach indicators).
- `OnTick()` — runs on every price update; intraday/entry logic lives here.
- `OnBar()` — runs on each completed bar (override when the strategy is bar-based, e.g.
  computing the prior session's high/low).
- `OnStop()` — teardown.

User-tunable inputs are `public` properties decorated with `[Parameter(...)]`; these surface
in the cTrader UI and the optimizer. Trading actions and market data come from inherited
members (`ExecuteMarketOrder`, `Positions`, `Symbol`, `Bars`, `MarketSeries`, `Print`, etc.).

`[Robot(AccessRights = AccessRights.None)]` means the bot cannot touch the file system or
network — keep it that way unless a feature genuinely requires elevated access.

## Conventions

- Spaces in the project/solution/file names are intentional (cTrader convention) — always
  quote paths in shell commands.
- `bin/`, `obj/`, `.idea/`, `*.user`, and generated `*.algo` files are git-ignored; commit
  only the `.cs`, `.csproj`, and `.sln`.