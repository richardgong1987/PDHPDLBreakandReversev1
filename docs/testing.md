# Testing Guide

Pure, framework-independent logic is unit-tested with **xUnit** under `tests/Pdhpdl.Tests`.
The cBot itself is not unit-tested here — it is validated in cTrader's backtester.

## Why the test project is separate

- It targets **`net10.0`** (the installed runtime), not the cBot's `net6.0`.
- It is **not** part of `PDHPDL Break and Reverse v1.sln`, so cTrader never tries to build it.
- It **links** pure source files via `<Compile Include>` instead of referencing the cBot
  project, so tests never pull in the `cTrader.Automate` / cAlgo.API dependency.

## Layout

Test files mirror the source folders, one test class per class under test:

```
tests/Pdhpdl.Tests/
  Risk/     RiskUtilTests.cs, PdhpdlRiskGuardTests.cs
  Signals/  PdhpdlSignalRulesTests.cs
  Orders/   PdhpdlOrderPlannerTests.cs
```

Each namespace mirrors its folder (`Pdhpdl.Tests.Risk`, `.Signals`, `.Orders`), so the test
explorer groups tests by the area they cover.

## Prerequisites

- .NET SDK installed. Check with:

  ```bash
  dotnet --version
  ```

## One command: build + test

`scripts/test.sh` builds the cBot (Release) and runs the unit tests in one step. Run it from
anywhere — it resolves the repo root itself:

```bash
./scripts/test.sh
```

## Run all tests

To run only the tests, from the repository root:

```bash
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj"
```

Expected tail of the output:

```text
Passed!  - Failed: 0, Passed: 24, Skipped: 0, Total: 24
```

## Useful variations

```bash
# More detail per test
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj" -v normal

# List the test names without running them
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj" --list-tests

# Run one test class
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj" \
  --filter "FullyQualifiedName~PdhpdlOrderPlannerTests"

# Run one test method by name
dotnet test "tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj" \
  --filter "Name=sizes_short_close_entry_geometry"
```

## Adding tests for new logic

1. Keep the new logic pure (no `cAlgo.API`, no I/O, no `DateTime.Now`).
2. Put the test in the folder that matches the source area (`Risk/`, `Signals/`, `Orders/`),
   in a class named after the unit under test (e.g. `PdhpdlOrderPlannerTests`), and name each
   test by behavior (e.g. `rejects_when_capped_volume_is_below_broker_minimum`).
3. If the class under test (or a pure data type it needs) lives in a new file, link it in the
   matching `ItemGroup` of `tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj`. Behavior classes go in
   the area group; data types from `Models/` go in the `Models under test` group:

   ```xml
   <Compile Include="..\..\PDHPDL Break and Reverse v1\Orders\YourClass.cs" Link="Orders\YourClass.cs" />
   <Compile Include="..\..\PDHPDL Break and Reverse v1\Models\YourModel.cs" Link="Models\YourModel.cs" />
   ```

   Never link a file that has `using cAlgo.API` (e.g. `CAlgoSymbolModel`) — it would pull the
   cTrader dependency into the tests.
