#!/bin/bash
set -euo pipefail

# Derived from this script's own location (repo/scripts/backtester/), so double-clicking the
# file works on any machine regardless of the shell's working directory.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOLUTION="$REPO_ROOT/PDHPDLBreakandReversev1.sln"
ROBOT_SOURCE="$REPO_ROOT/PDHPDLBreakandReversev1/PDHPDLBreakandReversev1.cs"

cd "$REPO_ROOT"

git pull --all
git reset --hard origin/main

# The release build ships with AccessRights.None so users installing the cBot are not asked to
# grant full machine access. The patch is applied to the working tree only and never committed:
# the reset --hard above restores the FullAccess source on the next run, and the trap below
# restores it as soon as this run finishes.
restore_robot_source() {
    git -C "$REPO_ROOT" checkout -- "$ROBOT_SOURCE" 2>/dev/null || true
}
trap restore_robot_source EXIT

if grep -q 'AccessRights = AccessRights\.FullAccess' "$ROBOT_SOURCE"; then
    sed -i '' 's/AccessRights = AccessRights\.FullAccess/AccessRights = AccessRights.None/' "$ROBOT_SOURCE"
    echo "Access rights patched to None for this release build."
elif grep -q 'AccessRights = AccessRights\.None' "$ROBOT_SOURCE"; then
    echo "Access rights already None in source; nothing to patch."
else
    # Never fall through to the build here. A silent no-match would ship whatever access rights
    # happen to be in source, which is the one thing this script exists to prevent.
    echo "ERROR: no 'AccessRights = AccessRights.<FullAccess|None>' found in $ROBOT_SOURCE" >&2
    echo "Refusing to build a release with unverified access rights." >&2
    exit 1
fi

dotnet build "$SOLUTION" -c Release
