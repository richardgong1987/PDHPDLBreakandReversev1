"""从 .env 文件读入环境相关配置（账户、路径、鉴权、回测资金/数据模式）。

账户、路径、鉴权等“环境相关”配置不写死在脚本里，而是从 .env 文件读取，这样同一套代码
用不同 env 文件即可切换环境（开发 / 生产）。
"""

# .env 必填项；DATA_MODE / BALANCE 选填，未填用默认值
REQUIRED_ENV_KEYS = ["AUTH_TOKEN", "CTRADER_BIN", "ALGO_PATH", "CTID", "ACCOUNT"]
DEFAULT_DATA_MODE = "m1"
DEFAULT_BALANCE = "10000"


class Config:
    """从 .env 文件读入的环境相关配置（账户、路径、鉴权、回测资金/数据模式）。"""

    def __init__(self, values):
        self.auth_token = values["AUTH_TOKEN"]
        self.ctrader_bin = values["CTRADER_BIN"]
        self.algo_path = values["ALGO_PATH"]
        self.ctid = values["CTID"]
        self.account = values["ACCOUNT"]
        self.data_mode = values.get("DATA_MODE") or DEFAULT_DATA_MODE
        self.balance = values.get("BALANCE") or DEFAULT_BALANCE


def parse_env_file(env_file):
    """把 .env 文件解析成 key->value 字典（忽略空行与 # 注释，去掉两侧引号）。"""
    if not env_file.exists():
        raise FileNotFoundError(
            f"找不到环境配置文件：{env_file}\n"
            "请复制 scripts/.env.example 为 .env（或 .env-prod）并填好里面的值。"
        )
    values = {}
    for raw_line in env_file.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def load_config(env_file):
    values = parse_env_file(env_file)
    missing = [key for key in REQUIRED_ENV_KEYS if not values.get(key)]
    if missing:
        raise ValueError(
            f"环境配置文件 {env_file} 缺少必填项：" + "、".join(missing)
        )
    return Config(values)
