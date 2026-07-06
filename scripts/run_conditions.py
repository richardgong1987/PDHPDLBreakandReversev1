#!/usr/bin/env python3
"""按 conditions.numbers 中的计划逐条运行 cTrader 历史回测（backtest）。

重要：使用的是 cTrader CLI 的 `backtest` 子命令（历史回测，跑完区间自动结束），
而不是 `run`（实时/前向运行，会一直连着实盘账户）。这是为了实现批量回测。

每一行是一条回测任务，按列名读取以下字段并映射成命令行参数：

    种类         -> --symbol
    周期         -> --period
    回撤开仓模式 -> --EntryModel  (Close=0, Pb25=1, Pb382=2, Pb50=3)
    止盈目标     -> --TakeProfitR
    起始日期     -> --start   (回测区间开始, DD/MM/YYYY, UTC)
    结束日期     -> --end     (回测区间结束, DD/MM/YYYY, UTC)
    最大浮盈     -> 暂时不处理

并把 种类/周期/回撤开仓模式/编号/止盈目标 拼成 cBot 日志文件名，例如：

    --FileName="XAUUSD-h1-Close-0-2.csv"

任务按顺序执行：等当前回测跑完，再执行下一条。
"""

import subprocess
import sys
from datetime import date, datetime
from pathlib import Path

from numbers_parser import Document

# --- 固定配置 ---------------------------------------------------------------

AUTH_TOKEN = "knS14gR_Zq2rqoes-NVEw9gvmOaj1fZ4m8AjsN5cgrw"
CTRADER_BIN = "/Applications/cTrader.app/Contents/MacOS/cTrader.Mac"
ALGO_PATH = "/Users/hanjingong/cAlgo/Sources/Robots/PDHPDL Break and Reverse v1.algo"
CONDITIONS_FILE = Path(__file__).resolve().parent / "conditions.numbers"

# 回测数据模式与初始资金（如需匹配图形界面回测，请调整成一致的值）
DATA_MODE = "m1"
BALANCE = "10000"

# 回撤开仓模式的名称 -> cBot 的 EntryModel 数值
ENTRY_MODEL_CODES = {
    "Close": 0,
    "Pb25": 1,
    "Pb382": 2,
    "Pb50": 3,
}

# 计划表列名（用列名匹配，避免依赖列顺序）
COLUMN_SYMBOL = "种类"
COLUMN_PERIOD = "周期"
COLUMN_ENTRY_MODEL = "回撤开仓模式"
COLUMN_TAKE_PROFIT = "止盈目标"
COLUMN_START_DATE = "起始日期"
COLUMN_END_DATE = "结束日期"

REQUIRED_COLUMNS = [
    COLUMN_SYMBOL,
    COLUMN_PERIOD,
    COLUMN_ENTRY_MODEL,
    COLUMN_TAKE_PROFIT,
    COLUMN_START_DATE,
    COLUMN_END_DATE,
]

# cBot 自定义参数（回测计划表覆盖之外的固定项，保持与手工命令一致）
CBOT_FIXED_PARAMS = {
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

# cTrader 账户/连接参数
CTID = "richardgong1988@gmail.com"
ACCOUNT = "5846740"


class ConditionRow:
    """计划表中的一条回测任务。"""

    def __init__(self, symbol, period, entry_model_name, take_profit_r, start_date, end_date):
        self.symbol = symbol
        self.period = period
        self.entry_model_name = entry_model_name
        self.take_profit_r = take_profit_r
        self.start_date = start_date
        self.end_date = end_date

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


def format_backtest_date(value, column_name):
    """把计划表里的日期单元格格式化成 cTrader 需要的 DD/MM/YYYY。

    计划表要求填 DD/MM/YYYY。为避免非法日期（如月份>12）被直接送进 cTrader
    才报错，这里先校验：Numbers 日期格按 DD/MM/YYYY 输出；文本必须能按
    DD/MM/YYYY 解析，否则明确指出是哪一列格式不对。
    """
    if isinstance(value, (datetime, date)):
        return value.strftime("%d/%m/%Y")
    text = str(value).strip()
    if not text:
        raise ValueError(f"计划表列「{column_name}」为空，请填写回测日期。")
    try:
        parsed = datetime.strptime(text, "%d/%m/%Y")
    except ValueError:
        raise ValueError(
            f"计划表列「{column_name}」的日期 {text!r} 不是合法的 DD/MM/YYYY 格式，"
            "请按 日/月/年 填写（例如 01/06/2026）。"
        )
    return parsed.strftime("%d/%m/%Y")


def resolve_column_indexes(header_row):
    """按列名定位每个必需列的下标，缺列时抛出清晰的错误。"""
    header = [str(cell).strip() if cell is not None else "" for cell in header_row]
    indexes = {}
    missing = []
    for column_name in REQUIRED_COLUMNS:
        if column_name in header:
            indexes[column_name] = header.index(column_name)
        else:
            missing.append(column_name)
    if missing:
        raise ValueError(
            "计划表缺少必需列：" + "、".join(missing) + "。\n"
            "请在 conditions.numbers 里补上这些列（起始日期/结束日期用于回测区间）。"
        )
    return indexes


def read_condition_rows(conditions_file):
    """读取计划表，返回有效的任务列表（跳过表头与空行）。"""
    document = Document(str(conditions_file))
    table = document.sheets[0].tables[0]
    rows = table.rows(values_only=True)
    if not rows:
        return []

    indexes = resolve_column_indexes(rows[0])

    tasks = []
    for row in rows[1:]:
        symbol = row[indexes[COLUMN_SYMBOL]]
        period = row[indexes[COLUMN_PERIOD]]
        entry_model_name = row[indexes[COLUMN_ENTRY_MODEL]]
        take_profit_r = row[indexes[COLUMN_TAKE_PROFIT]]
        start_cell = row[indexes[COLUMN_START_DATE]]
        end_cell = row[indexes[COLUMN_END_DATE]]

        if not all([symbol, period, entry_model_name]) or take_profit_r is None:
            continue
        if entry_model_name not in ENTRY_MODEL_CODES:
            raise ValueError(f"未知的回撤开仓模式: {entry_model_name!r}")

        start_date = format_backtest_date(start_cell, COLUMN_START_DATE)
        end_date = format_backtest_date(end_cell, COLUMN_END_DATE)
        tasks.append(
            ConditionRow(symbol, period, entry_model_name, take_profit_r, start_date, end_date)
        )
    return tasks


def build_command(task):
    """把一条任务翻译成完整的 cTrader backtest 命令。"""
    command = [
        "env",
        f"CTRADER_CLI_AUTHTOKEN={AUTH_TOKEN}",
        CTRADER_BIN,
        "backtest",
        ALGO_PATH,
        f"--ctid={CTID}",
        f"--account={ACCOUNT}",
        f"--symbol={task.symbol}",
        f"--period={task.period}",
        f"--start={task.start_date}",
        f"--end={task.end_date}",
        f"--data-mode={DATA_MODE}",
        f"--balance={BALANCE}",
        "--environment-variables",
        "--full-access",
    ]

    for name, value in CBOT_FIXED_PARAMS.items():
        command.append(f"--{name}={value}")

    command.append(f"--EntryModel={task.entry_model_code}")
    command.append(f"--TakeProfitR={task.take_profit_text}")
    command.append(f"--FileName={task.file_name}")
    return command


def run_task(task, index, total):
    print(
        f"\n[{index}/{total}] 回测 {task.file_name} "
        f"({task.start_date} -> {task.end_date})",
        flush=True,
    )
    result = subprocess.run(build_command(task))
    status = "完成" if result.returncode == 0 else f"失败(退出码 {result.returncode})"
    print(f"[{index}/{total}] {status}", flush=True)
    return result.returncode


def main():
    tasks = read_condition_rows(CONDITIONS_FILE)
    if not tasks:
        print("计划表中没有有效任务。")
        return 0

    print(f"共 {len(tasks)} 条回测任务，将按顺序逐条执行。")
    for index, task in enumerate(tasks, start=1):
        run_task(task, index, len(tasks))

    print("\n全部回测执行完毕。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
