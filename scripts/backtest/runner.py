"""串行 / 并发执行回测任务，并在每条完成后刷新汇总图 final_report.png。

默认逐条串行；用 jobs>1 时用有界线程池并发。子进程等待会释放 GIL，所以线程即可并行；
汇总图只在主线程刷新，避免多线程同时调用 matplotlib（pyplot 非线程安全）。
"""

import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

import report_summary

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


def refresh_final_report():
    """每条任务跑完后，用目录里现有的报告刷新汇总图。图表失败不应中断回测批次。"""
    try:
        image_path = report_summary.update_final_report(command.CBOT_OUTPUT_DIR)
    except Exception as error:  # noqa: BLE001 — 汇总图是附带产物，任何异常都不该拖垮回测
        print(f"*****汇总图刷新失败（已跳过）：{error}", flush=True)
        return

    if image_path is not None:
        print(f"*****汇总图已更新：{image_path}", flush=True)


def run_tasks_sequentially(tasks, config):
    """逐条串行执行（jobs 1）：保持顺序，每条跑完刷新汇总图。"""
    total = len(tasks)
    for index, task in enumerate(tasks, start=1):
        run_task(task, index, total, config)
        refresh_final_report()


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
            refresh_final_report()


def run_tasks(tasks, config, jobs):
    if jobs <= 1:
        run_tasks_sequentially(tasks, config)
    else:
        run_tasks_in_parallel(tasks, config, jobs)
