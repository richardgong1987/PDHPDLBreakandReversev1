#!/usr/bin/env python3
"""命令行入口：汇总输出目录里的回测报告 JSON，生成柱状图 final_report.png。

run_conditions.py 每跑完一条任务会调用 summary.update_final_report() 自动刷新；这个脚本
用来在不重跑回测的情况下手动刷新图片：

    python3 scripts/report_summary.py                 # 默认扫 ~/Documents
    python3 scripts/report_summary.py --dir <目录>

实现按职责拆在 summary/ 包里（metrics 读数 / chart 出图 / report 编排）。
"""

import argparse
import sys
from pathlib import Path

from summary import update_final_report


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
