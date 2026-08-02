"""串行 / 并发执行回测任务。汇总图 final_report.png 由调用方在全部跑完后生成一次。

默认逐条串行；显式传 jobs>1 时用有界线程池并发。子进程等待会释放 GIL，所以线程即可并行。
"""

import subprocess
from collections import namedtuple
from concurrent.futures import ThreadPoolExecutor, as_completed

from summary import update_final_report
from upload_reports import upload_zip

from . import command

# 一条没跑成的任务：index 用来对上运行时打的 [i/total]，方便回头翻日志。
TaskFailure = namedtuple("TaskFailure", ["index", "task", "return_code"])

# 默认串行。并发跑多个 cTrader 进程时，回测偶发在写 report-json 那一步抛
# InvalidOperationException: Message expected，且失败的那条会静默从汇总里消失，
# 得不偿失。需要提速时用 --jobs N 显式开启，自行确认报告份数与计划表行数一致。
DEFAULT_JOBS = 1


def run_task(task, index, total, config):
    print(
        f"\n[{index}/{total}] 回测 {task.file_name} "
        f"({task.start_date} -> {task.end_date})",
        flush=True,
    )
    result = subprocess.run(command.build_command(task, config))
    status = "完成" if result.returncode == 0 else f"失败(退出码 {result.returncode})"
    print(f"[{index}/{total}] {status}", flush=True)

    if result.returncode == 0:
        return None

    return TaskFailure(index, task, result.returncode)


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
    """逐条串行执行（jobs 1）：保持顺序。返回失败清单。"""
    total = len(tasks)
    failures = [run_task(task, index, total, config) for index, task in enumerate(tasks, start=1)]
    return [failure for failure in failures if failure is not None]


def run_tasks_in_parallel(tasks, config, jobs):
    """有界并发执行（jobs N）：最多同时跑 jobs 条。返回按计划表顺序排好的失败清单。"""
    total = len(tasks)
    failures = []
    with ThreadPoolExecutor(max_workers=jobs) as executor:
        futures = [
            executor.submit(run_task, task, index, total, config)
            for index, task in enumerate(tasks, start=1)
        ]
        for future in as_completed(futures):
            failure = future.result()  # 让 run_task 里的意外异常冒出来（正常失败只是非零返回码）
            if failure is not None:
                failures.append(failure)
    return sorted(failures, key=lambda failure: failure.index)


def run_tasks(tasks, config, jobs):
    """跑完整批，返回失败清单（空列表表示全部成功）。"""
    # 首条回测写盘前先清空并重建 trading_reports 目录，确保只保留本次批量生成的数据。
    print(f"清空并重建报告目录：{command.CBOT_OUTPUT_DIR}", flush=True)
    command.reset_output_dir()
    if jobs <= 1:
        return run_tasks_sequentially(tasks, config)
    return run_tasks_in_parallel(tasks, config, jobs)


def find_missing_reports(tasks):
    """找出没有产出报告 JSON 的任务。

    只看退出码不够：cTrader 偶发在写 report-json 那一步内部抛异常，进程仍可能以 0 退出，
    而汇总是扫目录里的 JSON 生成的——没写成的那条会直接从汇总里消失。这里按计划表逐条核对，
    把「跑了但没产出」的也揪出来。
    """
    return [task for task in tasks if not (command.CBOT_OUTPUT_DIR / task.report_file_name).exists()]


def print_batch_summary(tasks, failures, missing_reports):
    """把本批结论集中打印一次。中途那行 [5/7] 失败 很容易被回测日志淹掉。"""
    total = len(tasks)
    print(f"\n===== 本批结果：计划 {total} 条，成功 {total - len(failures)} 条，失败 {len(failures)} 条 =====")

    for failure in failures:
        print(f"  失败 [{failure.index}/{total}] 退出码 {failure.return_code}：{failure.task.file_name}")

    failed_names = {failure.task.report_file_name for failure in failures}
    for task in missing_reports:
        if task.report_file_name not in failed_names:
            print(f"  无报告（进程返回 0 但没写成，不会出现在汇总里）：{task.report_file_name}")

    if not failures and not missing_reports:
        print("  全部成功，报告份数与计划表一致。")
