"""读取 conditions.numbers 计划表，把每一行变成一个回测任务对象 ConditionRow。

计划表每一行是一条回测任务，按“列名”（不是列顺序）读取以下字段：

    种类             -> symbol
    周期             -> period
    回撤开仓模式     -> entry_model_name（映射成 EntryModel 数值）
    止盈目标         -> take_profit_r
    关键位连续最大次数 -> max_keylevel_times（同一关键位连续最大下单次数，0=不限制）
    策略模式         -> strategy_name（StrategyModel 成员名，按 RMA 排列强度隔离；留空=All）
    起始日期         -> start_date（DD/MM/YYYY, UTC）
    结束日期         -> end_date（DD/MM/YYYY, UTC）
"""

from datetime import date, datetime

from numbers_parser import Document

# 回撤开仓模式的名称 -> cBot 的 EntryModel 数值
ENTRY_MODEL_CODES = {
    "Close": 0,
    "Pb25": 1,
    "Pb382": 2,
    "Pb50": 3,
}

# 合法的「策略模式」取值，必须与 Models/StrategyModel.cs 的成员名一致（顺序无关）。
# CLI 传枚举时成员名和整数值都接受，这里用成员名：C# 枚举中间插入成员时整数值会整体偏移，
# 成员名不会。实测非法取值（拼错的名字、越界的整数）CLI 不报错、静默退回默认值 All，
# 所以这份白名单是唯一能挡住“回测跑的其实是 All”的防线。
STRATEGY_MODEL_NAMES = [
    "All",  # 全部条件，不作隔离
    "MultiplePosition",  # 只在允许多笔持仓的排列下开仓
]

# 计划表列名（用列名匹配，避免依赖列顺序）
COLUMN_SYMBOL = "种类"
COLUMN_PERIOD = "周期"
COLUMN_ENTRY_MODEL = "回撤开仓模式"
COLUMN_TAKE_PROFIT = "止盈目标"
COLUMN_MAX_KEYLEVEL_TIMES = "关键位连续最大次数"
COLUMN_STRATEGY = "策略模式"
COLUMN_START_DATE = "起始日期"
COLUMN_END_DATE = "结束日期"

REQUIRED_COLUMNS = [
    COLUMN_SYMBOL,
    COLUMN_PERIOD,
    COLUMN_ENTRY_MODEL,
    COLUMN_TAKE_PROFIT,
    COLUMN_MAX_KEYLEVEL_TIMES,
    COLUMN_STRATEGY,
    COLUMN_START_DATE,
    COLUMN_END_DATE,
]


class ConditionRow:
    """计划表中的一条回测任务。"""

    def __init__(
        self,
        symbol,
        period,
        entry_model_name,
        take_profit_r,
        max_keylevel_times,
        strategy_name,
        start_date,
        end_date,
    ):
        self.symbol = symbol
        self.period = period
        self.entry_model_name = entry_model_name
        self.take_profit_r = take_profit_r
        self.max_keylevel_times = max_keylevel_times
        self.strategy_name = strategy_name
        self.start_date = start_date
        self.end_date = end_date

    @property
    def entry_model_code(self):
        return ENTRY_MODEL_CODES[self.entry_model_name]

    @property
    def take_profit_text(self):
        """把 2.0 显示成 "2"，把 1.75 保留成 "1.75"。"""
        value = self.take_profit_r
        if float(value).is_integer():
            return str(int(value))
        return str(value)

    @property
    def max_keylevel_times_text(self):
        """文件名/命令行里用的次数文本，带 k 前缀便于人眼区分（k0 = 不限制）。"""
        return f"k{self.max_keylevel_times}"

    @property
    def file_name(self):
        return (
            f"{self.symbol}-{self.period}-{self.entry_model_name}-"
            f"{self.entry_model_code}-{self.take_profit_text}-"
            f"{self.max_keylevel_times_text}-{self.strategy_name}-"
            f"{to_compact_date(self.start_date)}-{to_compact_date(self.end_date)}.csv"
        )

    @property
    def report_file_name(self):
        """回测报告文件名：与 CSV 同名，只把 .csv 换成 .json。"""
        return self.file_name[:-len(".csv")] + ".json"


def format_backtest_date(value, column_name):
    """把计划表里的日期单元格格式化成 cTrader 需要的 DD/MM/YYYY。

    计划表要求填 DD/MM/YYYY。为避免非法日期（如月份>12）被直接送进 cTrader
    才报错，这里先校验：Numbers 日期格按 DD/MM/YYYY 输出；文本必须能按
    DD/MM/YYYY 解析，否则明确指出是哪一列格式不对。
    """
    if isinstance(value, (datetime, date)):
        return value.strftime("%d/%m/%Y")
    text = str(value).strip()
    if not text:
        raise ValueError(f"计划表列「{column_name}」为空，请填写回测日期。")
    try:
        parsed = datetime.strptime(text, "%d/%m/%Y")
    except ValueError:
        raise ValueError(
            f"计划表列「{column_name}」的日期 {text!r} 不是合法的 DD/MM/YYYY 格式，"
            "请按 日/月/年 填写（例如 01/06/2026）。"
        )
    return parsed.strftime("%d/%m/%Y")


def parse_max_keylevel_times(value):
    """把计划表单元格转成 cBot 需要的整数次数（Numbers 数字格会给出 2.0 这样的浮点）。"""
    if value is None or str(value).strip() == "":
        raise ValueError(f"计划表列「{COLUMN_MAX_KEYLEVEL_TIMES}」为空，请填 0（不限制）或正整数。")
    try:
        times = int(float(str(value).strip()))
    except ValueError:
        raise ValueError(
            f"计划表列「{COLUMN_MAX_KEYLEVEL_TIMES}」的值 {value!r} 不是整数，"
            "请填 0（不限制）或正整数。"
        )
    if times < 0:
        raise ValueError(
            f"计划表列「{COLUMN_MAX_KEYLEVEL_TIMES}」不能为负数：{value!r}。0 表示不限制。"
        )
    return times


def parse_strategy_name(value):
    """把计划表单元格转成 StrategyModel 的成员名；留空按 All（不作隔离）处理。"""
    name = "" if value is None else str(value).strip()
    if not name:
        return "All"
    if name not in STRATEGY_MODEL_NAMES:
        raise ValueError(
            f"计划表列「{COLUMN_STRATEGY}」的值 {value!r} 不是已知的策略模式。"
            "可填：" + "、".join(STRATEGY_MODEL_NAMES)
        )
    return name


def to_compact_date(backtest_date):
    """把 DD/MM/YYYY（传给 CLI 的格式）转成文件名用的紧凑 YYYYMMDD。"""
    return datetime.strptime(backtest_date, "%d/%m/%Y").strftime("%Y%m%d")


def resolve_column_indexes(header_row):
    """按列名定位每个必需列的下标，缺列时抛出清晰的错误。"""
    header = [str(cell).strip() if cell is not None else "" for cell in header_row]
    indexes = {}
    missing = []
    for column_name in REQUIRED_COLUMNS:
        if column_name in header:
            indexes[column_name] = header.index(column_name)
        else:
            missing.append(column_name)
    if missing:
        raise ValueError(
            "计划表缺少必需列：" + "、".join(missing) + "。\n"
            "请在 conditions.numbers 里补上这些列（起始日期/结束日期用于回测区间）。"
        )
    return indexes


def read_condition_rows(conditions_file):
    """读取计划表，返回有效的任务列表（跳过表头与空行）。"""
    document = Document(str(conditions_file))
    table = document.sheets[0].tables[0]
    rows = table.rows(values_only=True)
    if not rows:
        return []

    indexes = resolve_column_indexes(rows[0])

    tasks = []
    for row in rows[1:]:
        symbol = row[indexes[COLUMN_SYMBOL]]
        period = row[indexes[COLUMN_PERIOD]]
        entry_model_name = row[indexes[COLUMN_ENTRY_MODEL]]
        take_profit_r = row[indexes[COLUMN_TAKE_PROFIT]]
        max_keylevel_cell = row[indexes[COLUMN_MAX_KEYLEVEL_TIMES]]
        strategy_cell = row[indexes[COLUMN_STRATEGY]]
        start_cell = row[indexes[COLUMN_START_DATE]]
        end_cell = row[indexes[COLUMN_END_DATE]]

        if not all([symbol, period, entry_model_name]) or take_profit_r is None:
            continue
        if entry_model_name not in ENTRY_MODEL_CODES:
            raise ValueError(f"未知的回撤开仓模式: {entry_model_name!r}")

        max_keylevel_times = parse_max_keylevel_times(max_keylevel_cell)
        strategy_name = parse_strategy_name(strategy_cell)
        start_date = format_backtest_date(start_cell, COLUMN_START_DATE)
        end_date = format_backtest_date(end_cell, COLUMN_END_DATE)
        tasks.append(
            ConditionRow(
                symbol,
                period,
                entry_model_name,
                take_profit_r,
                max_keylevel_times,
                strategy_name,
                start_date,
                end_date,
            )
        )
    return tasks
