#!/usr/bin/env python3
"""按 conditions.numbers 中的计划逐条运行 cTrader 回测。

每一行是一条回测任务，读取以下列并映射成命令行参数：

    种类         -> --symbol
    周期         -> --period
    回撤开仓模式 -> --EntryModel  (Close=0, Pb25=1, Pb382=2, Pb50=3)
    止盈目标     -> --TakeProfitR
    最大浮盈     -> 暂时不处理

并把上述字段拼成日志文件名，例如：

    --FileName="XAUUSD-h1-Close-0-2.csv"

任务按顺序执行：等当前任务跑完，再执行下一条。
"""

import subprocess
import sys
from pathlib import Path

from numbers_parser import Document

# --- 固定配置 ---------------------------------------------------------------

AUTH_TOKEN = "knS14gR_Zq2rqoes-NVEw9gvmOaj1fZ4m8AjsN5cgrw"
CTRADER_BIN = "/Applications/cTrader.app/Contents/MacOS/cTrader.Mac"
ALGO_PATH = "/Users/hanjingong/cAlgo/Sources/Robots/PDHPDL Break and Reverse v1.algo"
CONDITIONS_FILE = Path(__file__).resolve().parent / "conditions.numbers"

# 回撤开仓模式的名称 -> cBot 的 EntryModel 数值
ENTRY_MODEL_CODES = {
    "Close": 0,
    "Pb25": 1,
    "Pb382": 2,
    "Pb50": 3,
}

# 除计划表覆盖之外的固定参数（保持与手工命令一致）
FIXED_ARGS = {
    "ctid": "richardgong1988@gmail.com",
    "account": "5846740",
    "port": "5034",
    "LineThickness": "3",
    "ResetTradeLogOnStart": "True",
    "RiskPct": "1",
    "RiskSafetyFactor": "1",
    "StopOffsetTicks": "15",
    "MinRiskPrice": "5",
    "NoNewOrdersStartHour": "4",
    "ForceCloseHour": "4",
    "ForceCloseMinute": "30",
    "ResumeTradingHour": "8",
    "FridayNoNewOrdersStartHour": "0",
    "FridayForceCloseHour": "3",
    "FridayForceCloseMinute": "30",
    "ShowDebugLogs": "False",
    "IsDebug": "False",
}


class ConditionRow:
    """计划表中的一条回测任务。"""

    def __init__(self, symbol, period, entry_model_name, take_profit_r):
        self.symbol = symbol
        self.period = period
        self.entry_model_name = entry_model_name
        self.take_profit_r = take_profit_r

    @property
    def entry_model_code(self):
        return ENTRY_MODEL_CODES[self.entry_model_name]

    @property
    def take_profit_text(self):
        """把 2.0 显示成 "2"，把 1.75 保留成 "1.75"。"""
        value = self.take_profit_r
        if float(value).is_integer():
            return str(int(value))
        return str(value)

    @property
    def file_name(self):
        return (
            f"{self.symbol}-{self.period}-{self.entry_model_name}-"
            f"{self.entry_model_code}-{self.take_profit_text}.csv"
        )


def read_condition_rows(conditions_file):
    """读取计划表，返回有效的任务列表（跳过表头与空行）。"""
    document = Document(str(conditions_file))
    table = document.sheets[0].tables[0]
    rows = table.rows(values_only=True)

    tasks = []
    for row in rows[1:]:  # 跳过表头
        symbol, period, entry_model_name = row[0], row[1], row[2]
        take_profit_r = row[3]
        if not all([symbol, period, entry_model_name]) or take_profit_r is None:
            continue
        if entry_model_name not in ENTRY_MODEL_CODES:
            raise ValueError(f"未知的回撤开仓模式: {entry_model_name!r}")
        tasks.append(ConditionRow(symbol, period, entry_model_name, take_profit_r))
    return tasks


def build_command(task):
    """把一条任务翻译成完整的 cTrader 运行命令。"""
    command = [
        "env",
        f"CTRADER_CLI_AUTHTOKEN={AUTH_TOKEN}",
        CTRADER_BIN,
        "run",
        ALGO_PATH,
    ]

    for name, value in FIXED_ARGS.items():
        command.append(f"--{name}={value}")

    command += ["--environment-variables", "--full-access"]
    command.append(f"--symbol={task.symbol}")
    command.append(f"--period={task.period}")
    command.append(f"--EntryModel={task.entry_model_code}")
    command.append(f"--TakeProfitR={task.take_profit_text}")
    command.append(f"--FileName={task.file_name}")
    command.append("--exit-on-stop")
    return command


def run_task(task, index, total):
    print(f"\n[{index}/{total}] 运行任务 -> {task.file_name}", flush=True)
    command = build_command(task)
    result = subprocess.run(command)
    if result.returncode != 0:
        print(f"[{index}/{total}] 任务失败，退出码 {result.returncode}", flush=True)
    else:
        print(f"[{index}/{total}] 任务完成", flush=True)
    return result.returncode


def main():
    tasks = read_condition_rows(CONDITIONS_FILE)
    if not tasks:
        print("计划表中没有有效任务。")
        return 0

    print(f"共 {len(tasks)} 条任务，将按顺序执行。")
    for index, task in enumerate(tasks, start=1):
        run_task(task, index, len(tasks))

    print("\n全部任务执行完毕。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
