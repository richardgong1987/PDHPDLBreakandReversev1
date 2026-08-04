# Backtest Plan From Parameter API Design

## 1. Business Purpose

The batch backtest plan (which parameter combinations to run) used to live in a local Apple
Numbers file, `scripts/backtester/conditions.numbers`. That made the plan a per-machine artifact:
it could not be edited from the trading admin, and every new cBot parameter needed three edits in
the script (a spreadsheet column, a `PARAMETERS` row, and possibly a `CBOT_FIXED_PARAMS` entry).

The backend now owns the plan and serves it at `CTRADER_PARAMETER_RECORDS_URL`. This change makes
that API the single source of truth so that adding, removing, or retuning a backtest parameter is
a backend-record change with no script edit.

## 2. Use Case

The operator runs a batch of cTrader historical backtests, one per parameter record published by
the backend parameter API.

## 3. Input Model

`GET $CTRADER_PARAMETER_RECORDS_URL` →

```json
{"code": 200, "success": true, "data": [
  {"recordId": 1, "symbol": "XAUUSD", "period": "m5",
   "parameterFields": [{"name": "TakeProfitR", "type": "double", "value": 2}, ...],
   "updateTime": "2026-08-04T17:03:38"}
]}
```

One record = one backtest task. `name` is the cBot's C# property name verbatim; `type` is
`double | int | bool | string | enum | date`.

## 4. Output Model

A `ConditionRow` per record, exposing `symbol`, `period`, `start_date`, `end_date`, `file_name`,
`report_file_name`, and `cli_args()` — unchanged from the spreadsheet-era interface, so `command.py`
and `runner.py` did not have to change shape.

## 5. Domain Rules

- Every `parameterFields` entry becomes exactly one `--<name>=<value>` argument. The script keeps
  no parameter whitelist — the set of parameters is the backend's decision.
- Values are formatted by declared `type`: `double` drops a trailing `.0`, `int`/`enum` become
  integers, `bool` becomes `True`/`False`, `date` converts ISO `YYYY-MM-DD` to the `DD/MM/YYYY`
  (UTC) cTrader requires. Unknown types pass through as text.
- A blank value means "not configured": the argument is omitted so the cBot uses its own default.
  (`--Name=` with an empty value has no documented CLI behaviour, so it is never emitted.)
- `FileName` from the API is dropped: the trade CSV path is decided by `command.py`, which pins it
  to an absolute path next to the report JSON.
- The report filename keeps the existing 9-field format, so `summary/naming.py` still parses it.
  Enum values are decoded back into member names (`EntryModel` 0 → `Close`, `Strategy` 0 → `All`)
  for readability.
- A record that cannot produce a filename (missing `symbol`, `period`, `Strategy`, `EntryModel`,
  `TakeProfitR`, `Nlock`, `start`, or `end`) is a hard error naming its `recordId` — not a silently
  skipped row.

## 6. Application Flow

1. `run_conditions.main` loads `Config` from `.env` (now requiring `CTRADER_PARAMETER_RECORDS_URL`).
2. `plan.read_condition_rows(url)` calls `records.fetch_parameter_records(url)` and maps each
   record to a `ConditionRow`, validating eagerly in the constructor.
3. `runner.run_tasks` executes each task via `command.build_command`, unchanged.
4. Summary, archive, and upload steps are unchanged.

## 7. Architecture Boundary

| Module | Responsibility | Reason to change |
|---|---|---|
| `backtest/records.py` | HTTP GET + envelope check | the API transport/envelope changes |
| `backtest/parameters.py` | field value → CLI text; enum name tables | the cBot's parameter types/enums change |
| `backtest/plan.py` | record → `ConditionRow`, report filename | what distinguishes one backtest changes |
| `backtest/command.py` | task + config → CLI command | the cTrader CLI changes |

`plan` depends on `records` and `parameters`; neither depends back. `command` and `runner` depend
only on the `ConditionRow` interface, not on where the plan came from.

## 8. Dependencies

Standard library only for the new code (`urllib.request`, `json`, `datetime`). `numbers-parser`
was removed from `scripts/requirements.txt`.

## 9. External Details

- Backend parameter API (`CTRADER_PARAMETER_RECORDS_URL`), 30 s timeout, no auth (matching
  `upload_reports.py`).
- cTrader CLI `backtest` subcommand.

## 10. Test Strategy

The value formatters (`to_number_text`, `to_whole_number_text`, `to_cli_date`, `to_compact_date`,
`enum_member_name`) and `ConditionRow` are pure given a record dict, so they are unit-testable with
a literal payload and no network. Verified manually against the live API: two records produce the
expected commands and report filenames, and `summary.naming.parse_report_name` round-trips them.

## 11. Risks and Trade-offs

- **Enum name tables are duplicated** between `parameters.py` and the C# enums. Keeping them buys
  readable report filenames (and an unchanged summary CSV); the cost is one place to update when an
  enum member is inserted. The tables are commented with the files they mirror.
- **Filename collisions**: two records differing only in a parameter that is not part of the
  filename overwrite each other's report. This risk existed with the spreadsheet too; `runner`'s
  `find_missing_reports` still reports the resulting gap at the end of the batch.
- **The batch now needs the network**. If the API is down, nothing runs — but there is no longer a
  local plan that could silently drift from the backend's.
- `scripts/backtester/conditions.numbers` is left on disk but is no longer read; it can be deleted.
