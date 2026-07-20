#!/bin/bash

REPO_ROOT=/Users/chenwanli/cAlgo/Sources/Robots/PDHPDL-Break-and-Reverse-v1
SOLUTION="$REPO_ROOT/PDHPDL Break and Reverse v1.sln"

cd $REPO_ROOT

dotnet build "$SOLUTION" -c Release


git pull --all
git reset --hard origin/master