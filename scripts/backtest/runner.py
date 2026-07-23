"""串行 / 并发执行回测任务。汇总图 final_report.png 由调用方在全部跑完后生成一次。

默认逐条串行；用 jobs>1 时用有界线程池并发。子进程等待会释放 GIL，所以线程即可并行。
"""

import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

from summary import update_final_report
from upload_reports import upload_zip

from . import command

# jobs 默认并发数：CPU 核数的一半（至少 1）。回测是 CPU/内存密集型，取一半核数是为了
# 在加速的同时给系统留余量，避免把机器抢满反而更慢。命令行可用 --jobs 覆盖。
DEFAULT_JOBS = max(1, (os.cpu_count() or 2) // 2)


def run_task(task, index, total, config):
    print(
        f"\n[{index}/{total}] 回测 {task.file_name} "
        f"({task.start_date} -> {task.end_date})",
        flush=True,
    )
    result = subprocess.run(command.build_command(task, config))
    status = "完成" if result.returncode == 0 else f"失败(退出码 {result.returncode})"
    print(f"[{index}/{total}] {status}", flush=True)
    return result.returncode


def generate_final_report():
    """全部回测结束后，扫描输出目录里的所有报告，生成一次汇总图和汇总表。生成失败不应中断批次。

    返回 SummaryOutputs（含各产物路径与共用时间戳）供打包复用；无报告或生成失败时返回 None。
    """
    try:
        outputs = update_final_report(command.CBOT_OUTPUT_DIR)
    except Exception as error:  # noqa: BLE001 — 汇总产物是附带结果，任何异常都不该拖垮回测
        print(f"*****汇总报告生成失败（已跳过）：{error}", flush=True)
        return None

    if outputs is not None:
        print(f"*****汇总图已生成：{outputs.chart_path}", flush=True)
        print(f"*****汇总表已生成：{outputs.csv_path}", flush=True)
        print(f"*****元数据已生成：{outputs.metadata_path}", flush=True)
    return outputs


def archive_reports(timestamp=None):
    """把报告目录打包成 zip 存档，返回 zip 路径供上传复用；无产物或失败时返回 None。

    传入汇总产物的时间戳，让 zip 后缀与 final_report_<ts>.png 一致（trading_reports_<ts>.zip）。
    打包是附带产物，任何异常都不该拖垮回测批次。
    """
    try:
        archive_path = command.archive_output_dir(timestamp)
    except Exception as error:  # noqa: BLE001
        print(f"*****报告打包失败（已跳过）：{error}", flush=True)
        return None

    print(f"*****报告已打包：{archive_path}", flush=True)
    return archive_path


def upload_report_archive(upload_url, archive_path):
    """把刚打包的报告 zip 上传到后端。上传是附带产物，任何异常都不该拖垮调用方。

    上传地址随环境不同，由调用方从 .env 的 REPORT_UPLOAD_URL 读入；未配置则跳过。
    只上传本次生成的这一个 zip（archive_path），不扫描目录，避免误传旧存档。
    """
    if not upload_url:
        print("*****未配置 REPORT_UPLOAD_URL，跳过上传。", flush=True)
        return
    if archive_path is None:
        print("*****没有可上传的报告 zip，跳过上传。", flush=True)
        return

    try:
        upload_zip(upload_url, archive_path)
    except Exception as error:  # noqa: BLE001
        print(f"*****报告上传失败（已跳过）：{error}", flush=True)


def run_tasks_sequentially(tasks, config):
    """逐条串行执行（jobs 1）：保持顺序。"""
    total = len(tasks)
    for index, task in enumerate(tasks, start=1):
        run_task(task, index, total, config)


def run_tasks_in_parallel(tasks, config, jobs):
    """有界并发执行（jobs N）：最多同时跑 jobs 条。"""
    total = len(tasks)
    with ThreadPoolExecutor(max_workers=jobs) as executor:
        futures = [
            executor.submit(run_task, task, index, total, config)
            for index, task in enumerate(tasks, start=1)
        ]
        for future in as_completed(futures):
            future.result()  # 让 run_task 里的意外异常冒出来（正常失败只是非零返回码）


def run_tasks(tasks, config, jobs):
    # 首条回测写盘前先清空并重建 trading_reports 目录，确保只保留本次批量生成的数据。
    print(f"清空并重建报告目录：{command.CBOT_OUTPUT_DIR}", flush=True)
    command.reset_output_dir()
    if jobs <= 1:
        run_tasks_sequentially(tasks, config)
    else:
        run_tasks_in_parallel(tasks, config, jobs)
