"""编排层：扫描目录里的报告，生成汇总图 final_report.png 和汇总表 final_summary_report.csv。"""

from collections import namedtuple
from pathlib import Path

from .chart import render_report_chart
from .metadata import METADATA_NAME, write_metadata_json
from .metrics import load_report_frame
from .table import write_summary_csv

IMAGE_NAME = "final_report.png"
CSV_NAME = "final_summary_report.csv"

# 一次汇总产出的三个文件：柱状图 + CSV 表 + 可入库的 metadata.json
SummaryOutputs = namedtuple("SummaryOutputs", ["chart_path", "csv_path", "metadata_path"])


def update_final_report(output_dir, image_path=None):
    """扫描目录里的报告，生成汇总图和汇总表。

    无可用报告时跳过并返回 None；否则返回 SummaryOutputs(chart_path, csv_path)。
    image_path 缺省时落在 output_dir/final_report.png；CSV 固定为 output_dir/final_summary_report.csv。
    """
    frame = load_report_frame(output_dir)
    if frame.empty:
        return None

    output_dir = Path(output_dir)
    chart_path = Path(image_path) if image_path else output_dir / IMAGE_NAME
    csv_path = output_dir / CSV_NAME
    metadata_path = output_dir / METADATA_NAME

    render_report_chart(frame, chart_path)
    write_summary_csv(frame, csv_path)
    write_metadata_json(frame, metadata_path)
    return SummaryOutputs(chart_path=chart_path, csv_path=csv_path, metadata_path=metadata_path)
