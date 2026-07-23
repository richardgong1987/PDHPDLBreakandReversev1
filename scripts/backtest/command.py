"""把一条回测任务翻译成 cTrader CLI 的 backtest 命令。

重要：用的是 cTrader CLI 的 `backtest` 子命令（历史回测，跑完区间自动结束），不是 `run`
（实时/前向运行，会一直连着实盘账户）。任务字段 + 环境配置 + 固定参数拼成完整命令行。
"""

import shutil
from datetime import datetime
from pathlib import Path

# 所有报告统一落在「我的文档」下的 trading_reports 子目录：交易 CSV、回测报告 --report-json、
# 以及批量结束后的汇总产物都在这里，方便集中留档、不再把 ~/Documents 根目录弄乱。
# 交易 CSV 由 cBot(C#) 写盘，通过给 --FileName 传绝对路径把它也导向这里（见 build_command）。
# report-json 与 CSV 同名（仅把 .csv 换成 .json），一条回测的 CSV 和 report 成对存放、互相对应。
CBOT_OUTPUT_DIR = Path.home() / "Documents" / "trading_reports"


def reset_output_dir():
    """删除并重建报告输出目录：先清掉上次批量的残留文件，再建空目录接收本次生成的数据。

    只在批量回测开始前调用（run_tasks）。report_summary.py 复用已有报告，不应调用它，否则会把
    要汇总的 JSON 一并删掉。
    """
    if CBOT_OUTPUT_DIR.exists():
        shutil.rmtree(CBOT_OUTPUT_DIR)
    CBOT_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


def archive_output_dir(timestamp=None):
    """把报告目录打包成带时间戳的 zip 存档，供以后使用；返回 zip 路径。

    zip 放在报告目录的上级（~/Documents），刻意不放进 trading_reports 内部，否则下次批量回测
    reset_output_dir 会把它一并删掉。带时间戳使多次批量的存档可以并存、互不覆盖。

    timestamp 传汇总产物用的那个 YYYYMMDDHHmmss（来自 update_final_report），让 zip 后缀与
    final_report_<ts>.png 完全一致：trading_reports_<ts>.zip。没有汇总产物时回落到当前时间。
    """
    timestamp = timestamp or datetime.now().strftime("%Y%m%d%H%M%S")
    base_name = CBOT_OUTPUT_DIR.parent / f"{CBOT_OUTPUT_DIR.name}_{timestamp}"
    archive_path = shutil.make_archive(
        str(base_name), "zip", root_dir=str(CBOT_OUTPUT_DIR.parent), base_dir=CBOT_OUTPUT_DIR.name
    )
    return Path(archive_path)

# cBot 自定义参数（回测计划表覆盖之外的固定项，保持与手工命令一致）
# 名称必须与 cBot 的 C# 属性名逐字一致（cTrader CLI 按属性名匹配，不是按中文显示名）。
# 枚举成员名和整数值 CLI 都接受（实测）。这里 MaSource / EntryModel 沿用整数值，
# Strategy 用成员名（枚举中间插入成员时整数值会整体偏移，成员名不会）：
#   MaSource -> MovingAverageSourceModel: HigherTimeFrame=0, ChartTimeFrame=1
# 注意：非法枚举取值 CLI 不报错，会静默退回参数默认值，所以取值由 plan.py 校验。
CBOT_FIXED_PARAMS = {
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
        f"--data-mode=ticks",
        "--commission=30",
        "--balance=10000",
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
    command.append(f"--MaxKeylevelTimes={task.max_keylevel_times}")
    command.append(f"--Strategy={task.strategy_name}")
    # 传绝对路径：cBot 内部会 Path.Combine(我的文档, FileName)，第二参为绝对路径时 .NET 直接返回它，
    # 于是交易 CSV 也落到 trading_reports，与 report-json 同目录。
    command.append(f"--FileName={CBOT_OUTPUT_DIR / task.file_name}")
    command.append(f"--report-json={CBOT_OUTPUT_DIR / task.report_file_name}")
    return command
