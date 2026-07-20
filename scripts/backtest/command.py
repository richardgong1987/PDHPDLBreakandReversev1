"""把一条回测任务翻译成 cTrader CLI 的 backtest 命令。

重要：用的是 cTrader CLI 的 `backtest` 子命令（历史回测，跑完区间自动结束），不是 `run`
（实时/前向运行，会一直连着实盘账户）。任务字段 + 环境配置 + 固定参数拼成完整命令行。
"""

from pathlib import Path

# cBot 把交易 CSV 写到「我的文档」(~/Documents)。回测报告 --report-json 与 CSV 同目录、
# 同名（仅把 .csv 后缀换成 .json），方便一条回测的 CSV 和 report 成对存放、互相对应。
CBOT_OUTPUT_DIR = Path.home() / "Documents"

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
    command.append(f"--FileName={task.file_name}")
    command.append(f"--report-json={CBOT_OUTPUT_DIR / task.report_file_name}")
    return command
