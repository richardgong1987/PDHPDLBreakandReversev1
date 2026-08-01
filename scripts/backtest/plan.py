"""读取 conditions.numbers 计划表，把每一行变成一个回测任务对象 ConditionRow。

计划表每一行是一条回测任务，按“列名”（不是列顺序）读取。读哪些列、每列怎么校验、
对应哪个 cBot 参数，全部登记在 parameters.PARAMETERS——加减参数请改那张表，不要改这里。

报告文件名的拼法仍写在本文件的 ConditionRow.file_name 里：文件名只放需要区分回测结果的
字段，与参数表不是一一对应的关系。
"""

from datetime import datetime

from numbers_parser import Document

from .parameters import PARAMETERS, PARAMETER_COLUMNS

COLUMN_SYMBOL = "种类"
COLUMN_PERIOD = "周期"
COLUMN_ENTRY_MODEL = "回撤开仓模式"
COLUMN_TAKE_PROFIT = "止盈目标"
COLUMN_NLOCK = "N次止损Lock"
COLUMN_STRATEGY = "策略模式"
COLUMN_START_DATE = "起始日期"
COLUMN_END_DATE = "结束日期"

# 这几列留空说明这一行还没填完（Numbers 表尾常有空行），整行跳过而不是报错。
UNFINISHED_ROW_COLUMNS = [
    COLUMN_SYMBOL,
    COLUMN_PERIOD,
    COLUMN_ENTRY_MODEL,
    COLUMN_TAKE_PROFIT,
]


class ConditionRow:
    """计划表中的一条回测任务。参数值按列名存放，校验在 Parameter.parse 里做。"""

    def __init__(self, cells):
        self._cells = dict(cells)
        self._values = {
            parameter.column: parameter.parse(cells[parameter.column])
            for parameter in PARAMETERS
        }

    def cli_args(self):
        """这条任务的 cBot 参数命令行片段，顺序与 PARAMETERS 一致。"""
        return [
            parameter.cli_arg(self._values[parameter.column]) for parameter in PARAMETERS
        ]

    @property
    def symbol(self):
        return self._values[COLUMN_SYMBOL]

    @property
    def period(self):
        return self._values[COLUMN_PERIOD]

    @property
    def start_date(self):
        return self._values[COLUMN_START_DATE]

    @property
    def end_date(self):
        return self._values[COLUMN_END_DATE]

    @property
    def file_name(self):
        return (
            f"{self.symbol}-{self.period}-{self._entry_model_name}-"
            f"{self._values[COLUMN_ENTRY_MODEL]}-{self._values[COLUMN_TAKE_PROFIT]}-"
            f"n{self._values[COLUMN_NLOCK]}-{self._values[COLUMN_STRATEGY]}-"
            f"{to_compact_date(self.start_date)}-{to_compact_date(self.end_date)}.csv"
        )

    @property
    def report_file_name(self):
        """回测报告文件名：与 CSV 同名，只把 .csv 换成 .json。"""
        return self.file_name[: -len(".csv")] + ".json"

    @property
    def _entry_model_name(self):
        """文件名里用模式名（Close/Pb25/…），命令行里用它对应的数值。"""
        return str(self._cells[COLUMN_ENTRY_MODEL]).strip()


def to_compact_date(backtest_date):
    """把 DD/MM/YYYY（传给 CLI 的格式）转成文件名用的紧凑 YYYYMMDD。"""
    return datetime.strptime(backtest_date, "%d/%m/%Y").strftime("%Y%m%d")


def resolve_column_indexes(header_row):
    """按列名定位每个必需列的下标，缺列时抛出清晰的错误。"""
    header = [str(cell).strip() if cell is not None else "" for cell in header_row]
    indexes = {}
    missing = []
    for column_name in PARAMETER_COLUMNS:
        if column_name in header:
            indexes[column_name] = header.index(column_name)
        else:
            missing.append(column_name)
    if missing:
        raise ValueError(
            "计划表缺少必需列：" + "、".join(missing) + "。\n"
            "请在 conditions.numbers 里补上这些列（列名需与参数表里的写法逐字一致）。"
        )
    return indexes


def read_condition_rows(conditions_file):
    """读取计划表，返回有效的任务列表（跳过表头与未填完的行）。"""
    document = Document(str(conditions_file))
    table = document.sheets[0].tables[0]
    rows = table.rows(values_only=True)
    if not rows:
        return []

    indexes = resolve_column_indexes(rows[0])

    tasks = []
    for row in rows[1:]:
        cells = {column: row[indexes[column]] for column in PARAMETER_COLUMNS}
        if _is_unfinished_row(cells):
            continue
        tasks.append(ConditionRow(cells))
    return tasks


def _is_unfinished_row(cells):
    return any(_is_blank(cells[column]) for column in UNFINISHED_ROW_COLUMNS)


def _is_blank(cell):
    return cell is None or str(cell).strip() == ""
