"""汇总 cTrader 回测报告（--report-json 生成的 JSON），画成柱状图 final_report.png。

每个报告 JSON 对应一条回测任务（一组参数 + 区间）。从中读两项指标：

    胜率     = tradeStatistics.winningTrades.all / totalTrades.all
    盈利金额 = main.netProfit

run_conditions.py 每跑完一条任务就调用 update_final_report()，重新扫描输出目录里的
所有报告并刷新图片，所以图会随着批量回测的进度逐步长出来。

也可单独运行来手动刷新图片（不重跑回测）：

    python3 scripts/report_summary.py                 # 默认扫 ~/Documents
    python3 scripts/report_summary.py --dir <目录>
"""

import argparse
import json
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")  # 无界面后端：脚本里存图不需要弹窗
import matplotlib.pyplot as plt  # noqa: E402
import pandas as pd  # noqa: E402

IMAGE_NAME = "final_report.png"

# 柱状图配色
PROFIT_POSITIVE_COLOR = "#2e7d32"
PROFIT_NEGATIVE_COLOR = "#c62828"
WIN_RATE_COLOR = "#1565c0"


def read_report_stats(report_path):
    """从单个报告 JSON 读出胜率与净利润；不是回测报告就返回 None。"""
    try:
        with open(report_path, encoding="utf-8") as report_file:
            report = json.load(report_file)
    except (json.JSONDecodeError, OSError):
        return None

    if not isinstance(report, dict) or "main" not in report or "tradeStatistics" not in report:
        return None

    trade_statistics = report["tradeStatistics"]
    total_trades = trade_statistics.get("totalTrades", {}).get("all", 0) or 0
    winning_trades = trade_statistics.get("winningTrades", {}).get("all", 0) or 0
    win_rate = (winning_trades / total_trades * 100.0) if total_trades else 0.0

    return {
        "report": report_path.stem,
        "net_profit": report["main"].get("netProfit", 0.0),
        "win_rate": win_rate,
        "total_trades": total_trades,
    }


def load_report_frame(output_dir):
    """扫描目录下所有回测报告 JSON，汇总成按报告名排序的 DataFrame。"""
    records = []
    for report_path in sorted(Path(output_dir).glob("*.json")):
        stats = read_report_stats(report_path)
        if stats is not None:
            records.append(stats)

    columns = ["report", "net_profit", "win_rate", "total_trades"]
    frame = pd.DataFrame(records, columns=columns)
    return frame.sort_values("report").reset_index(drop=True)


def render_report_chart(frame, image_path):
    """把汇总表画成上下两幅柱状图（净利润 / 胜率）并存成 PNG。"""
    labels = frame["report"].tolist()  # 用完整文件名（不含扩展名）当 X 轴标签
    positions = range(len(frame))
    figure_width = max(12.0, len(frame) * 0.45)

    figure, (profit_axes, win_rate_axes) = plt.subplots(2, 1, figsize=(figure_width, 9), sharex=True)

    profit_colors = [PROFIT_POSITIVE_COLOR if value >= 0 else PROFIT_NEGATIVE_COLOR for value in frame["net_profit"]]
    profit_axes.bar(positions, frame["net_profit"], color=profit_colors)
    profit_axes.axhline(0, color="black", linewidth=0.8)
    profit_axes.set_ylabel("Net profit")
    profit_axes.set_title("Backtest summary — net profit & win rate per report")
    profit_axes.grid(axis="y", linestyle=":", alpha=0.4)

    win_rate_axes.bar(positions, frame["win_rate"], color=WIN_RATE_COLOR)
    win_rate_axes.set_ylabel("Win rate (%)")
    win_rate_axes.set_ylim(0, 100)
    win_rate_axes.grid(axis="y", linestyle=":", alpha=0.4)
    win_rate_axes.set_xticks(list(positions))
    win_rate_axes.set_xticklabels(labels, rotation=90, fontsize=7)

    figure.tight_layout()
    # bbox_inches="tight" 保证竖排的完整文件名标签不会被裁掉
    figure.savefig(image_path, dpi=150, bbox_inches="tight")
    plt.close(figure)


def update_final_report(output_dir, image_path=None):
    """扫描目录里的报告，重画 final_report.png。无报告时跳过，返回图片路径或 None。"""
    resolved_image_path = Path(image_path) if image_path else Path(output_dir) / IMAGE_NAME
    frame = load_report_frame(output_dir)

    if frame.empty:
        return None

    render_report_chart(frame, resolved_image_path)
    return resolved_image_path


def parse_args(argv):
    parser = argparse.ArgumentParser(description="汇总回测报告 JSON，生成 final_report.png。")
    parser.add_argument(
        "--dir",
        default=str(Path.home() / "Documents"),
        help="报告 JSON 所在目录（默认 ~/Documents，与 cBot 输出一致）。",
    )
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    image_path = update_final_report(args.dir)

    if image_path is None:
        print(f"目录里没有可用的回测报告 JSON：{args.dir}")
        return 0

    print(f"已生成汇总图：{image_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
