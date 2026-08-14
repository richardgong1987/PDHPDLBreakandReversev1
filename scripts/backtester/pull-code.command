#!/bin/bash

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOLUTION="$REPO_ROOT/PDHPDLBreakandReversev1.sln"

cd $REPO_ROOT

git pull --all
git reset --hard origin/master

dotnet build "$SOLUTION" -c Release

