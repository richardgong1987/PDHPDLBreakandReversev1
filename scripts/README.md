# 批量回测脚本（run_conditions.py）

按 `conditions.numbers` 里的计划表，逐条运行 cTrader 历史回测（`backtest`），
每条跑完自动进入下一条，各自把交易明细写到独立的 CSV。

## 依赖安装

```bash
pip install -r scripts/requirements.txt
```

第三方依赖：`numbers-parser`（读 `.numbers` 表格）、`pandas` + `matplotlib`
（汇总回测报告、画柱状图）；其余是 Python 标准库。

> 包名是 `numbers-parser`（连字符），代码里 `import numbers_parser`（下划线），是同一个包。

## 环境配置（.env）

账户、路径、鉴权等环境相关配置放在 `.env` 文件里，脚本不再写死，也不用为不同
环境复制脚本——只维护 env 文件即可：

```bash
cp scripts/.env.example scripts/.env        # 首次：生成开发配置并填值
```

必填项：`AUTH_TOKEN`、`CTRADER_BIN`、`ALGO_PATH`、`CTID`、`ACCOUNT`；
选填：`DATA_MODE`（默认 `m1`）、`BALANCE`（默认 `10000`）。

`.env` 和 `.env-prod` 含鉴权 token，已在 `.gitignore` 里忽略，不会提交；
只有 `.env.example` 模板会进版本库。

## 使用步骤

1. 用 Numbers 打开 `backtester/conditions.numbers`，按需增删行、改参数。
2. 运行：

   ```bash
   python3 scripts/run_conditions.py                          # 默认读 scripts/.env
   python3 scripts/run_conditions.py --env-file scripts/.env-prod   # 用生产配置
   python3 scripts/run_conditions.py --jobs 4                 # 最多同时跑 4 条
   python3 scripts/run_conditions.py --jobs 1                 # 逐条串行
   ```

3. 每条结果 CSV 生成在 `~/Documents/`。

运行前会先做校验（env 缺文件/缺必填项、计划表缺列、日期格式非法）并明确报错，
不会带着错误配置去跑。

### 并发回测（--jobs）

`--jobs N` 控制同时跑几条回测，**默认 = CPU 核数的一半**（给系统留余量），`--jobs 1`
恢复逐条串行。回测是 CPU/内存密集型，并发数超过物理核数一般不会更快、反而互相抢资源。

第一次并发跑某个品种前，建议先 `--jobs 1` 跑一条把 m1 数据缓存好，再放大并发，避免多个
进程同时下载同一份数据时冲突。汇总图 `final_report.png` 只在主线程刷新，并发下也安全。

> cTrader 官方文档说明回测引擎支持并行跑多个回测进程，但 CLI 层面未明确背书并发用法；
> 首次并发建议先小样（2～3 条）验证报告/CSV 正常，再放大。

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

由计划表字段拼接而成（日期用紧凑的 `YYYYMMDD`）：

```
<种类>-<周期>-<回撤开仓模式>-<EntryModel值>-<止盈目标>-<起始日期>-<结束日期>.csv
```

例：`XAUUSD-h1-Close-0-2-20260601-20260630.csv`。文件写到 `~/Documents/`（由 cBot 自身的日志器决定路径）。

每条回测还会额外生成一个 **回测报告 JSON**（`--report-json`），与 CSV **同目录、同名**，
只把后缀换成 `.json`：

```
~/Documents/XAUUSD-h1-Close-0-2-20260601-20260630.json
```

这样每条回测的 CSV（交易明细）和 report.json（回测统计）成对存放、文件名一一对应。

## 汇总柱状图（final_report.png）

每跑完一条任务，脚本会扫描输出目录里**所有**回测报告 JSON，用 `pandas` 汇总成表，再用
`matplotlib` 画成上下两幅柱状图并导出到 `~/Documents/final_report.png`：

- **上图**：每份报告的净利润（`main.netProfit`），盈利绿色、亏损红色。
- **下图**：每份报告的胜率（`winningTrades.all / totalTrades.all`）。
- X 轴标签是每份报告的完整文件名（不含扩展名），一眼就能对上是哪组参数/区间。

图会随批量回测进度逐步长出来，跑到一半也能随时打开看当前结果。汇总逻辑在
`report_summary.py`，也可单独运行来手动刷新图片（不重跑回测）：

```bash
python3 scripts/report_summary.py                 # 默认扫 ~/Documents
python3 scripts/report_summary.py --dir <目录>    # 指定报告目录
```

> 注意：图会汇总目录里**所有**报告 JSON，包括历史遗留的。若只想看某一批，先清掉目录里
> 旧的 `*.json` 再跑。

## 可调项

环境相关（在 `.env` / `.env-prod` 里改）：

- `AUTH_TOKEN` / `CTRADER_BIN` / `ALGO_PATH` / `CTID` / `ACCOUNT`（账户、路径、鉴权）
- `BALANCE`（初始资金，默认 `10000`）
- `DATA_MODE`（回测数据模式，默认 `m1`；可选 `open`、`m1-csv`）

策略参数（在 `backtest/command.py` 的 `CBOT_FIXED_PARAMS` 里改）：

这里的 key 必须与 cBot 的 C# 属性名逐字一致（cTrader CLI 按属性名匹配，不是按中文显示名）。
当前固定项与 cBot 参数一一对应：

| CBOT_FIXED_PARAMS | 默认值 | 含义 |
|---|---|---|
| `Strategy` | `0` | 策略模式（枚举整数：AB=0, A=1, B=2） |
| `ResetTradeLogOnStart` | `True` | 启动时清空交易记录 CSV |
| `RiskPct` | `1` | 每笔交易风险百分比 |
| `RiskSafetyFactor` | `1` | 风险安全系数 |
| `StopOffsetTicks` | `15` | 止损偏移点数 |
| `MinStopLossPips` | `5` | 最小止损点数（Pips） |
| `SaturdayForceCloseHour` | `5` | 周六强制平仓小时（日本时间） |
| `SaturdayForceCloseMinute` | `30` | 周六强制平仓分钟 |
| `ShowMovingAverages` | `True` | 是否绘制均线（RMA 1 + RMA 2） |
| `MaSource` | `0` | 均线来源（枚举整数：HigherTimeFrame=0, ChartTimeFrame=1） |
| `MaFastPeriod` | `13` | 均线周期 RMA 1（快） |
| `MaSlowPeriod` | `55` | 均线周期 RMA 2（慢） |
| `MaTimeFrameMinutes` | `120` | 均线周期（分钟） |
| `ShowDebugLogs` | `False` | 展示调试日志 |
| `IsDebug` | `False` | debug 调试 |

> 均线参数不只影响绘图：`MaSource`/`MaFastPeriod`/`MaSlowPeriod`/`MaTimeFrameMinutes` 会喂给
> RMA 均线序列，而多空信号会用快/慢 RMA 的相对位置过滤方向，所以它们直接影响回测出的交易。
> 枚举（`Strategy`、`MaSource`）按整数值传，与 `EntryModel` 的做法一致。

未列入的 cBot 参数（如 `NewsBlackoutWindows`）不传，回测时走 cBot 自身默认值；需要固定时再加进
`CBOT_FIXED_PARAMS`。`EntryModel`、`TakeProfitR`、`FileName` 由计划表逐行覆盖，不放在这里。

## 代码结构

命令行入口 `run_conditions.py` 只负责“组合”（解析参数 + 装配各模块），具体逻辑按职责拆在
`backtest/` 包里，各管一件事、互不耦合：

```
run_conditions.py     命令行入口（组合根：parse_args + main）
report_summary.py     汇总报告 JSON、画 final_report.png（可单独运行）
backtest/
  config.py           读 .env，产出 Config（账户/路径/鉴权/回测资金/数据模式）
  plan.py             读 conditions.numbers，产出回测任务 ConditionRow
  command.py          任务 + 配置 -> cTrader CLI 命令（含 CBOT_FIXED_PARAMS）
  runner.py           串行/并发执行任务，每条完成后刷新汇总图
```

依赖方向：`run_conditions → runner → command`，以及 `run_conditions → config / plan`。
`config` / `plan` / `command` 是叶子模块，不反向依赖 runner。

## 关键设计说明

- 用的是 CLI 的 **`backtest`** 子命令（历史回测，跑完即止），**不是 `run`**
  （`run` 是实时/前向运行，会一直连着实盘账户、不会自己结束）。
- 命令带 **`--exit-on-stop`**：backtest 跑完后进程不会自动退出（会空转），
  加这个标志才能让它结束，脚本才能进入下一条。
- 命令带 **`--full-access`**：允许 cBot 写出自己的交易 CSV。
- 日期统一按 **DD/MM/YYYY（UTC）** 传给 cTrader；脚本会校验格式。

官方 CLI 文档：<https://help.ctrader.com/ctrader-algo/documentation/ctrader-cli/>
