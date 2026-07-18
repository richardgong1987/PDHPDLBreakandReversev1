#!/usr/bin/env bash

cd "$(dirname "$0")/.." || exit 1

pip3 install -r requirements.txt

python3 run_conditions.py --env-file .env
