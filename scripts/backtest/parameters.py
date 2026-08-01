"""计划表列 -> cTrader CLI 参数 的对照表。

这是回测参数的唯一登记处。加一个参数就在 PARAMETERS 里添一行，删一个就删掉那一行，
plan.py 会自动把它列为必需列并校验，command.py 会自动把它拼进命令行——两个文件都不用改。

    Parameter("N次止损Lock", "--Nlock", to_value=whole_number)
     ^ conditions.numbers 的列名   ^ cBot 的 CLI 参数名   ^ 可选：单元格 -> 参数值

to_value 不填就原样传（Numbers 的文本格直接可用）。需要换算或校验时才写一个，
非法值请抛 ValueError，plan.py 会补上列名再报出来。

CLI 参数名必须与 cBot 的 C# 属性名逐字一致（cTrader CLI 按属性名匹配，不是按中文显示名）。
注意：非法枚举取值 CLI 不报错，会静默退回参数默认值——所以取值校验只能靠这里的 to_value。
"""

from datetime import date, datetime

# 回撤开仓模式的名称 -> cBot 的 EntryModel 数值
ENTRY_MODEL_CODES = {
    "Close": 0,
    "Pb25": 1,
    "Pb382": 2,
    "Pb50": 3,
}

# 合法的「策略模式」取值，必须与 Models/StrategyModel.cs 的成员名一致（顺序无关）。
# 枚举成员名和整数值 CLI 都接受，这里用成员名：C# 枚举中间插入成员时整数值会整体偏移，
# 成员名不会。这份白名单是唯一能挡住“回测跑的其实是 All”的防线。
STRATEGY_MODEL_NAMES = [
    "All",  # 全部条件，不作隔离
    "MultiplePosition",  # 只在允许多笔持仓的排列下开仓
]


def plain_text(cell):
    """非空文本，去掉首尾空白。"""
    text = "" if cell is None else str(cell).strip()
    if not text:
        raise ValueError("为空，请填写。")
    return text


def entry_model_code(cell):
    """回撤开仓模式的名称 -> EntryModel 数值。"""
    name = plain_text(cell)
    if name not in ENTRY_MODEL_CODES:
        raise ValueError(
            f"的值 {cell!r} 不是已知的回撤开仓模式。可填：" + "、".join(ENTRY_MODEL_CODES)
        )
    return ENTRY_MODEL_CODES[name]


def strategy_name(cell):
    """StrategyModel 的成员名；留空按 All（不作隔离）处理。"""
    name = "" if cell is None else str(cell).strip()
    if not name:
        return "All"
    if name not in STRATEGY_MODEL_NAMES:
        raise ValueError(
            f"的值 {cell!r} 不是已知的策略模式。可填：" + "、".join(STRATEGY_MODEL_NAMES)
        )
    return name


def number_text(cell):
    """数字文本：把 2.0 显示成 "2"，把 1.75 保留成 "1.75"（Numbers 数字格给的是浮点）。"""
    if cell is None or str(cell).strip() == "":
        raise ValueError("为空，请填写数字。")
    try:
        number = float(str(cell).strip())
    except ValueError:
        raise ValueError(f"的值 {cell!r} 不是数字。") from None
    return str(int(number)) if number.is_integer() else str(number)


def whole_number(cell):
    """0 或正整数（Numbers 数字格会给出 2.0 这样的浮点）。"""
    if cell is None or str(cell).strip() == "":
        raise ValueError("为空，请填 0 或正整数。")
    try:
        count = int(float(str(cell).strip()))
    except ValueError:
        raise ValueError(f"的值 {cell!r} 不是整数，请填 0 或正整数。") from None
    if count < 0:
        raise ValueError(f"不能为负数：{cell!r}。")
    return count


def backtest_date(cell):
    """把单元格格式化成 cTrader 需要的 DD/MM/YYYY。

    计划表要求填 DD/MM/YYYY。为避免非法日期（如月份>12）被直接送进 cTrader 才报错，
    这里先校验：Numbers 日期格按 DD/MM/YYYY 输出；文本必须能按 DD/MM/YYYY 解析。
    """
    if isinstance(cell, (datetime, date)):
        return cell.strftime("%d/%m/%Y")
    text = "" if cell is None else str(cell).strip()
    if not text:
        raise ValueError("为空，请填写回测日期。")
    try:
        parsed = datetime.strptime(text, "%d/%m/%Y")
    except ValueError:
        raise ValueError(
            f"的日期 {text!r} 不是合法的 DD/MM/YYYY 格式，请按 日/月/年 填写（例如 01/06/2026）。"
        ) from None
    return parsed.strftime("%d/%m/%Y")


class Parameter:
    """计划表的一列 = cBot 的一个 CLI 参数。"""

    def __init__(self, column, flag, to_value=None):
        self.column = column
        self.flag = flag
        self._to_value = to_value or plain_text

    def parse(self, cell):
        """单元格 -> 参数值；非法值补上列名后抛出，让人一眼看出该改哪一列。"""
        try:
            return self._to_value(cell)
        except ValueError as error:
            raise ValueError(f"计划表列「{self.column}」{error}") from error

    def cli_arg(self, value):
        return f"{self.flag}={value}"


# ↓↓↓ 加参数就在这里添一行，删参数就删掉那一行 ↓↓↓
PARAMETERS = [
    Parameter("种类", "--symbol"),
    Parameter("周期", "--period"),
    Parameter("回撤开仓模式", "--EntryModel", to_value=entry_model_code),
    Parameter("止盈目标", "--TakeProfitR", to_value=number_text),
    Parameter("N次止损Lock", "--Nlock", to_value=whole_number),
    Parameter("策略模式", "--Strategy", to_value=strategy_name),
    Parameter("起始日期", "--start", to_value=backtest_date),
    Parameter("结束日期", "--end", to_value=backtest_date),
]

PARAMETER_COLUMNS = [parameter.column for parameter in PARAMETERS]
