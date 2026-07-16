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

并把 种类/周期/回撤开仓模式/编号/止盈目标/起始日期/结束日期 拼成 cBot 日志文件名，例如：

    --FileName="XAUUSD-h1-Close-0-2-20260601-20260630.csv"

任务按顺序执行：等当前回测跑完，再执行下一条。
"""

import argparse
import subprocess
import sys
from datetime import date, datetime
from pathlib import Path

from numbers_parser import Document

# --- 路径与环境配置 ---------------------------------------------------------
#
# 账户、路径、鉴权等“环境相关”配置不写死在脚本里，而是从 .env 文件读取，
# 这样同一个脚本用不同 env 文件即可切换环境：
#     python3 run_conditions.py                          # 默认读 scripts/.env
#     python3 run_conditions.py --env-file scripts/.env-prod   # 读生产配置

SCRIPTS_DIR = Path(__file__).resolve().parent
DEFAULT_ENV_FILE = SCRIPTS_DIR / ".env"
CONDITIONS_FILE = SCRIPTS_DIR / "backtester/conditions.numbers"

# .env 必填项；DATA_MODE / BALANCE 选填，未填用默认值
REQUIRED_ENV_KEYS = ["AUTH_TOKEN", "CTRADER_BIN", "ALGO_PATH", "CTID", "ACCOUNT"]
DEFAULT_DATA_MODE = "m1"
DEFAULT_BALANCE = "10000"


class Config:
    """从 .env 文件读入的环境相关配置（账户、路径、鉴权、回测资金/数据模式）。"""

    def __init__(self, values):
        self.auth_token = values["AUTH_TOKEN"]
        self.ctrader_bin = values["CTRADER_BIN"]
        self.algo_path = values["ALGO_PATH"]
        self.ctid = values["CTID"]
        self.account = values["ACCOUNT"]
        self.data_mode = values.get("DATA_MODE") or DEFAULT_DATA_MODE
        self.balance = values.get("BALANCE") or DEFAULT_BALANCE


def parse_env_file(env_file):
    """把 .env 文件解析成 key->value 字典（忽略空行与 # 注释，去掉两侧引号）。"""
    if not env_file.exists():
        raise FileNotFoundError(
            f"找不到环境配置文件：{env_file}\n"
            "请复制 scripts/.env.example 为 .env（或 .env-prod）并填好里面的值。"
        )
    values = {}
    for raw_line in env_file.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def load_config(env_file):
    values = parse_env_file(env_file)
    missing = [key for key in REQUIRED_ENV_KEYS if not values.get(key)]
    if missing:
        raise ValueError(
            f"环境配置文件 {env_file} 缺少必填项：" + "、".join(missing)
        )
    return Config(values)

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
# 名称必须与 cBot 的 C# 属性名逐字一致（cTrader CLI 按属性名匹配，不是按中文显示名）。
# 枚举按整数值传（与 EntryModel 一致）：
#   Strategy -> StrategyModel: AB=0, A=1, B=2
#   MaSource -> MovingAverageSourceModel: HigherTimeFrame=0, ChartTimeFrame=1
CBOT_FIXED_PARAMS = {
    "Strategy": "0",
    "ResetTradeLogOnStart": "True",
    "RiskPct": "1",
    "RiskSafetyFactor": "1",
    "StopOffsetTicks": "15",
    "MinStopLossPips": "5",
    "SaturdayForceCloseHour": "5",
    "SaturdayForceCloseMinute": "30",
    "ShowMovingAverages": "True",
    "MaSource": "0",
    "MaFastPeriod": "13",
    "MaSlowPeriod": "55",
    "MaTimeFrameMinutes": "120",
    "ShowDebugLogs": "False",
    "IsDebug": "False",
}


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
            f"{self.entry_model_code}-{self.take_profit_text}-"
            f"{to_compact_date(self.start_date)}-{to_compact_date(self.end_date)}.csv"
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


def to_compact_date(backtest_date):
    """把 DD/MM/YYYY（传给 CLI 的格式）转成文件名用的紧凑 YYYYMMDD。"""
    return datetime.strptime(backtest_date, "%d/%m/%Y").strftime("%Y%m%d")


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


def build_command(task, config):
    """把一条任务翻译成完整的 cTrader backtest 命令。"""
    command = [
        "env",
        f"CTRADER_CLI_AUTHTOKEN={config.auth_token}",
        config.ctrader_bin,
        "backtest",
        config.algo_path,
        f"--ctid={config.ctid}",
        f"--account={config.account}",
        f"--symbol={task.symbol}",
        f"--period={task.period}",
        f"--start={task.start_date}",
        f"--end={task.end_date}",
        f"--data-mode={config.data_mode}",
        f"--balance={config.balance}",
        "--environment-variables",
        "--full-access",
        # backtest 跑完后不会自己退出（进程会空转），--exit-on-stop 让它结束，
        # 否则 subprocess.run 永远等待，批量无法进入下一条。
        "--exit-on-stop",
    ]

    for name, value in CBOT_FIXED_PARAMS.items():
        command.append(f"--{name}={value}")

    command.append(f"--EntryModel={task.entry_model_code}")
    command.append(f"--TakeProfitR={task.take_profit_text}")
    command.append(f"--FileName={task.file_name}")
    return command


def run_task(task, index, total, config):
    print(
        f"\n[{index}/{total}] 回测 {task.file_name} "
        f"({task.start_date} -> {task.end_date})",
        flush=True,
    )
    result = subprocess.run(build_command(task, config))
    status = "完成" if result.returncode == 0 else f"失败(退出码 {result.returncode})"
    print(f"[{index}/{total}] {status}", flush=True)
    return result.returncode


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="按 conditions.numbers 计划表逐条运行 cTrader 历史回测。"
    )
    parser.add_argument(
        "--env-file",
        default=str(DEFAULT_ENV_FILE),
        help="环境配置文件路径（默认 scripts/.env；生产用 --env-file scripts/.env-prod）",
    )
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    config = load_config(Path(args.env_file))

    tasks = read_condition_rows(CONDITIONS_FILE)
    if not tasks:
        print("计划表中没有有效任务。")
        return 0

    print(f"共 {len(tasks)} 条回测任务，将按顺序逐条执行。")
    for index, task in enumerate(tasks, start=1):
        run_task(task, index, len(tasks), config)

    print("\n全部回测执行完毕。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
