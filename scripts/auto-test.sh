#!/usr/bin/env bash
#
# auto-test.sh — build the cBot, run a cTrader backtest headlessly, save the report.
#
#   Pipeline:  dotnet build (Release) -> ctrader-cli backtest -> report in an output dir
#
# The output directory is customizable (defaults to ~/Documents). The backtest runs
# through the cTrader CLI, either a native `ctrader-cli` on PATH or the official Linux
# Docker image (the default on macOS). The report + Events.json/Log.txt land in the
# output directory.
#
# ------------------------------------------------------------------------------------
# One-time setup (secrets stay OUT of the repo):
#
#   1. Save your cTrader account password to a file, e.g. ~/.ctrader/password.pwd
#   2. Create scripts/auto-test.env (git-ignored) with your account:
#
#        CTRADER_CTID=your_ctrader_id
#        CTRADER_ACCOUNT=1234567
#        CTRADER_PWD_FILE=$HOME/.ctrader/password.pwd
#
# Then just run:  ./scripts/auto-test.sh              # report -> ~/Documents
#            or:  ./scripts/auto-test.sh -o /tmp/bt   # custom output dir
#
# NOTE: the cTrader CLI is relatively new and flag names can differ between versions.
#       If a run fails, check your version with:  ctrader-cli backtest --help
#       and adjust the BT_FLAGS / env below.
# ------------------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Load local, git-ignored config if present (holds credentials / overrides).
[[ -f "$SCRIPT_DIR/auto-test.env" ]] && source "$SCRIPT_DIR/auto-test.env"

# ---- Fixed project facts -----------------------------------------------------------
SOLUTION="PDHPDL Break and Reverse v1.sln"
ALGO_NAME="PDHPDL Break and Reverse v1.algo"
CONFIG="Release"
ALGO_PATH="$REPO_ROOT/PDHPDL Break and Reverse v1/bin/$CONFIG/net6.0/$ALGO_NAME"

# ---- Output location (customizable; defaults to ~/Documents) -----------------------
OUT_DIR="${AUTO_TEST_OUT:-$HOME/Documents}"
SKIP_BUILD=0

usage() {
    cat <<'EOF'
auto-test.sh — build the cBot, run a cTrader backtest headlessly, save the report.

  Pipeline:  dotnet build (Release) -> ctrader-cli backtest -> report in an output dir

Usage: auto-test.sh [-o|--out <dir>] [--skip-build] [-h|--help]

  -o, --out <dir>   Where to write the report (default: ~/Documents, or $AUTO_TEST_OUT)
  --skip-build      Reuse the existing .algo (skip dotnet build) to re-run a backtest fast

One-time setup (secrets stay OUT of the repo) — create scripts/auto-test.env with:
  CTRADER_CTID=your_ctrader_id
  CTRADER_ACCOUNT=1234567
  CTRADER_PWD_FILE=$HOME/.ctrader/password.pwd
Backtest params (BT_SYMBOL, BT_PERIOD, BT_START, BT_END, ...) can be overridden there too.

NOTE: the cTrader CLI is new; flag names can differ between versions. If a run fails,
      check yours with `ctrader-cli backtest --help` and adjust BT_FLAGS / env.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -o|--out) OUT_DIR="$2"; shift 2 ;;
        --skip-build) SKIP_BUILD=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 1 ;;
    esac
done

# ---- Backtest parameters (override via env or auto-test.env) ------------------------
BT_SYMBOL="${BT_SYMBOL:-XAUUSD}"
BT_PERIOD="${BT_PERIOD:-m15}"
BT_START="${BT_START:-01/01/2026 00:00}"    # dd/MM/yyyy HH:mm
BT_END="${BT_END:-30/06/2026 23:59}"
BT_BALANCE="${BT_BALANCE:-10000}"
BT_COMMISSION="${BT_COMMISSION:-30}"
BT_SPREAD="${BT_SPREAD:-1}"
BT_DATA_MODE="${BT_DATA_MODE:-m1}"          # m1 pulls tick/m1 data from the server

CTRADER_IMAGE="${CTRADER_IMAGE:-ghcr.io/spotware/ctrader-console:latest}"
CTRADER_ENTRYPOINT="${CTRADER_ENTRYPOINT:-ctrader-cli}"

# ---- 1. Build ----------------------------------------------------------------------
if [[ "$SKIP_BUILD" -eq 0 ]]; then
    echo "==> Building cBot ($SOLUTION, $CONFIG)"
    dotnet build "$REPO_ROOT/$SOLUTION" -c "$CONFIG"
fi
[[ -f "$ALGO_PATH" ]] || { echo "ERROR: .algo not found at $ALGO_PATH" >&2; exit 1; }

# ---- 2. Validate backtest prerequisites --------------------------------------------
: "${CTRADER_CTID:?Set CTRADER_CTID (your cTrader ID) in scripts/auto-test.env or the environment}"
: "${CTRADER_ACCOUNT:?Set CTRADER_ACCOUNT (broker account id) in scripts/auto-test.env or the environment}"
CTRADER_PWD_FILE="${CTRADER_PWD_FILE:-$HOME/.ctrader/password.pwd}"
[[ -f "$CTRADER_PWD_FILE" ]] || { echo "ERROR: password file not found: $CTRADER_PWD_FILE" >&2; exit 1; }

BT_FLAGS=(
    --symbol="$BT_SYMBOL"
    --period="$BT_PERIOD"
    --start="$BT_START"
    --end="$BT_END"
    --data-mode="$BT_DATA_MODE"
    --balance="$BT_BALANCE"
    --commission="$BT_COMMISSION"
    --spread="$BT_SPREAD"
    --ctid="$CTRADER_CTID"
    --account="$CTRADER_ACCOUNT"
)

mkdir -p "$OUT_DIR"

# ---- 3. Backtest -------------------------------------------------------------------
echo "==> Backtesting $BT_SYMBOL $BT_PERIOD ($BT_START -> $BT_END)"
echo "    output: $OUT_DIR"

if command -v ctrader-cli >/dev/null 2>&1; then
    # Native CLI (e.g. Windows or a local install).
    ctrader-cli backtest "$ALGO_PATH" "${BT_FLAGS[@]}" \
        --report="$OUT_DIR/report.html" \
        --pwd-file="$CTRADER_PWD_FILE"
elif command -v docker >/dev/null 2>&1; then
    # Docker image. The container writes Events.json/Log.txt next to the .algo, so the
    # .algo is copied into the (mounted, writable) output dir and removed afterwards.
    PWD_DIR="$(cd "$(dirname "$CTRADER_PWD_FILE")" && pwd)"
    PWD_FILE_NAME="$(basename "$CTRADER_PWD_FILE")"
    cp "$ALGO_PATH" "$OUT_DIR/$ALGO_NAME"
    trap 'rm -f "$OUT_DIR/$ALGO_NAME"' EXIT

    docker run --rm \
        --entrypoint "$CTRADER_ENTRYPOINT" \
        -v "$OUT_DIR":/work \
        -v "$PWD_DIR":/secrets:ro \
        "$CTRADER_IMAGE" \
        backtest "/work/$ALGO_NAME" "${BT_FLAGS[@]}" \
        --report="/work/report.html" \
        --pwd-file="/secrets/$PWD_FILE_NAME"
else
    echo "ERROR: need either 'ctrader-cli' on PATH or Docker installed." >&2
    exit 1
fi

# ---- 4. Summary --------------------------------------------------------------------
REPORT="$OUT_DIR/report.html"
echo
echo "==> Done. Report: $REPORT"

if [[ -f "$REPORT" ]] && command -v python3 >/dev/null 2>&1; then
    python3 - "$REPORT" <<'PY' || true
import json, re, sys
html = open(sys.argv[1], encoding="utf-8", errors="ignore").read()
m = re.search(r'id="backtesting-report">(\{.*?\})\s*</script>', html, re.S)
if not m:
    sys.exit(0)
d = json.loads(m.group(1)); main = d["main"]; ts = d.get("tradeStatistics", {})
def g(x, k): return (x or {}).get(k)
print("---- backtest summary ----")
print(f"  net profit : {main.get('netProfit')}  ({main.get('depositAsset')})   ROI: {main.get('roi')}%")
print(f"  equity     : {main.get('startingCapital')} -> {main.get('endingEquity')}")
tt = ts.get("totalTrades", {}); wt = ts.get("winningTrades", {}); lt = ts.get("losingTrades", {})
allt = g(tt, "all")
if allt:
    print(f"  trades     : {allt}  (win {g(wt,'all')} / loss {g(lt,'all')}, {round(100*g(wt,'all')/allt,1)}% win)")
    print(f"  by side    : long net {g(ts.get('netProfit'),'long')}  short net {g(ts.get('netProfit'),'short')}")
eq = d.get("equity", {})
print(f"  max equity drawdown : {eq.get('maxEquityDrawdownPercent')}%")
PY
fi
