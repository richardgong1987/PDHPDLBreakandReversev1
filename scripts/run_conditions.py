#!/usr/bin/env python3
"""按 conditions.numbers 中的计划批量运行 cTrader 历史回测（backtest）。

命令行入口（组合根）：解析参数、读环境配置、读计划表，再交给 runner 逐条/并发执行。
具体实现按职责拆在 backtest/ 包里：

    backtest/config   —— 从 .env 读环境配置（账户/路径/鉴权/回测资金/数据模式）
    backtest/plan     —— 读 conditions.numbers 计划表，产出回测任务 ConditionRow
    backtest/command  —— 把任务翻译成 cTrader CLI 的 backtest 命令
    backtest/runner   —— 串行/并发执行任务，并刷新汇总图 final_report.png

用法：

    python3 run_conditions.py                          # 默认读 scripts/.env
    python3 run_conditions.py --env-file scripts/.env-prod   # 读生产配置
    python3 run_conditions.py --jobs 4                 # 最多同时跑 4 条（默认 = 核数一半）
"""

import argparse
import sys
from pathlib import Path

from backtest.config import load_config
from backtest.plan import read_condition_rows
from backtest.runner import DEFAULT_JOBS, archive_reports, generate_final_report, run_tasks

SCRIPTS_DIR = Path(__file__).resolve().parent
DEFAULT_ENV_FILE = SCRIPTS_DIR / ".env"
CONDITIONS_FILE = SCRIPTS_DIR / "backtester/conditions.numbers"


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="按 conditions.numbers 计划表逐条运行 cTrader 历史回测。"
    )
    parser.add_argument(
        "--env-file",
        default=str(DEFAULT_ENV_FILE),
        help="环境配置文件路径（默认 scripts/.env；生产用 --env-file scripts/.env-prod）",
    )
    parser.add_argument(
        "--jobs",
        type=int,
        default=DEFAULT_JOBS,
        help=f"并发回测的任务数（默认 {DEFAULT_JOBS} = CPU 核数的一半）；设为 1 则逐条串行。",
    )
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    config = load_config(Path(args.env_file))

    tasks = read_condition_rows(CONDITIONS_FILE)
    if not tasks:
        print("计划表中没有有效任务。")
        return 0

    jobs = max(1, args.jobs)
    if jobs > 1:
        print(f"共 {len(tasks)} 条回测任务，最多并发 {jobs} 条执行。")
    else:
        print(f"共 {len(tasks)} 条回测任务，将按顺序逐条执行。")

    run_tasks(tasks, config, jobs)

    # 全部跑完后，扫描所有报告生成一次汇总图（不再每条任务都刷新一次）
    outputs = generate_final_report()

    # 汇总产物齐了，再把整个报告目录打包成 zip 存档，供以后使用；
    # 复用汇总产物的时间戳，让 zip 后缀与 final_report_<ts>.png 一致。
    archive_reports(outputs.timestamp if outputs is not None else None)

    print("\n全部回测执行完毕。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
