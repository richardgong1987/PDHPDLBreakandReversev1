#!/bin/bash

REPO_ROOT=/Users/chenwanli/cAlgo/Sources/Robots/PDHPDLBreakandReversev1
SOLUTION="$REPO_ROOT/PDHPDLBreakandReversev1.sln"

cd $REPO_ROOT

git pull --all
git reset --hard origin/master

dotnet build "$SOLUTION" -c Release

