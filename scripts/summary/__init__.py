"""汇总 cTrader 回测报告（--report-json 的 JSON），画成柱状图 final_report.png。

按职责拆成三层：

- metrics ：把报告 JSON 读成一张 DataFrame（胜率 / 净利润）——数据层。
- chart   ：把 DataFrame 画成上下两幅柱状图 PNG——展示层。
- report  ：扫描目录 -> 汇总 -> 出图的编排（组合根用的公开入口）。

命令行入口在上一层的 report_summary.py。
"""

from .report import IMAGE_NAME, update_final_report

__all__ = ["update_final_report", "IMAGE_NAME"]
