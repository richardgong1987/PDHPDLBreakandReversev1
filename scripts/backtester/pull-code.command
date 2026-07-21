#!/bin/bash

REPO_ROOT=/Users/chenwanli/cAlgo/Sources/Robots/PDHPDL-Break-and-Reverse-v1
SOLUTION="$REPO_ROOT/PDHPDL Break and Reverse v1.sln"

cd $REPO_ROOT

git pull --all
git reset --hard origin/master

dotnet build "$SOLUTION" -c Release

