# 批量回测脚本（run_conditions.py）

按 `conditions.numbers` 里的计划表，逐条运行 cTrader 历史回测（`backtest`），
每条跑完自动进入下一条，各自把交易明细写到独立的 CSV。

## 依赖安装

```bash
pip install -r scripts/requirements.txt
```

只依赖一个第三方库 `numbers-parser`（读 `.numbers` 表格）；其余是 Python 标准库。

> 包名是 `numbers-parser`（连字符），代码里 `import numbers_parser`（下划线），是同一个包。

## 使用步骤

1. 用 Numbers 打开 `scripts/conditions.numbers`，按需增删行、改参数。
2. 运行：

   ```bash
   python3 scripts/run_conditions.py
   ```

3. 脚本按顺序逐条回测，每条结果 CSV 生成在 `~/Documents/`。

运行前会先做校验（缺列、日期格式非法）并明确报错，不会带着错误配置去跑。

## 计划表（conditions.numbers）列说明

用**列名**匹配，列的顺序随意；表头行必须包含以下列名：

| 列名 | 作用 | 映射到的参数 | 备注 |
|---|---|---|---|
| 种类 | 交易品种 | `--symbol` | 如 `XAUUSD`、`EURUSD` |
| 周期 | 时间周期 | `--period` | 如 `m5`、`m15`、`h1`、`H4` |
| 回撤开仓模式 | 入场模式 | `--EntryModel` | 见下方映射 |
| 止盈目标 | 止盈倍数(R) | `--TakeProfitR` | `2.0` 会写成 `2`，`1.75` 保留 |
| 起始日期 | 回测开始 | `--start` | **DD/MM/YYYY**（日/月/年，UTC） |
| 结束日期 | 回测结束 | `--end` | **DD/MM/YYYY** |
| 最大浮盈 | —— | 暂不处理 | 读取但不使用 |

**回撤开仓模式 → EntryModel 映射：**

| 名称 | 值 |
|---|---|
| Close | 0 |
| Pb25 | 1 |
| Pb382 | 2 |
| Pb50 | 3 |

空行、必填字段缺失的行会被自动跳过。

## 输出 CSV 文件名

由计划表字段拼接而成：

```
<种类>-<周期>-<回撤开仓模式>-<EntryModel值>-<止盈目标>.csv
```

例：`XAUUSD-h1-Close-0-2.csv`。文件写到 `~/Documents/`（由 cBot 自身的日志器决定路径）。

## 可调固定项（脚本顶部常量）

在 `run_conditions.py` 开头可以改：

- `BALANCE`（初始资金，默认 `10000`）
- `DATA_MODE`（回测数据模式，默认 `m1`；可选 `open`、`m1-csv`）
- `CBOT_FIXED_PARAMS`（RiskPct、StopOffsetTicks 等所有非计划表覆盖的 cBot 参数）
- `AUTH_TOKEN` / `CTID` / `ACCOUNT`（账户与鉴权）

## 关键设计说明

- 用的是 CLI 的 **`backtest`** 子命令（历史回测，跑完即止），**不是 `run`**
  （`run` 是实时/前向运行，会一直连着实盘账户、不会自己结束）。
- 命令带 **`--exit-on-stop`**：backtest 跑完后进程不会自动退出（会空转），
  加这个标志才能让它结束，脚本才能进入下一条。
- 命令带 **`--full-access`**：允许 cBot 写出自己的交易 CSV。
- 日期统一按 **DD/MM/YYYY（UTC）** 传给 cTrader；脚本会校验格式。

官方 CLI 文档：<https://help.ctrader.com/ctrader-algo/documentation/ctrader-cli/>
