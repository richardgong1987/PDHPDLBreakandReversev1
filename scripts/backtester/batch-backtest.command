#!/usr/bin/env bash

cd "$(dirname "$0")/.." || exit 1

python3 run_conditions.py --env-file .env
