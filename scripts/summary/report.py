"""编排层：扫描目录里的报告，汇总并重画 final_report.png。"""

from pathlib import Path

from .chart import render_report_chart
from .metrics import load_report_frame

IMAGE_NAME = "final_report.png"


def update_final_report(output_dir, image_path=None):
    """扫描目录里的报告，重画 final_report.png。

    无可用报告时跳过并返回 None；否则返回生成的图片路径。
    image_path 缺省时落在 output_dir/final_report.png。
    """
    frame = load_report_frame(output_dir)
    if frame.empty:
        return None

    resolved_image_path = Path(image_path) if image_path else Path(output_dir) / IMAGE_NAME
    render_report_chart(frame, resolved_image_path)
    return resolved_image_path
