trader-cli backtest <cbot.algo> [<params.cbotset>] \
  --start=<开始时间> --end=<结束时间> \
  --data-mode=<m1|m1-csv|open> [--data-file=<路径>] \
  [--balance=<初始资金>] [--commission=<佣金>] [--spread=<点差>] \
  [--report=<输出路径>] \
  --ctid=<你的cTraderID> --pwd-file=<密码文件> --account=<账户ID> \
  --symbol=<品种> --period=<周期>


ctrader-cli backtest 'C:\test\sample martingale.algo' C:\test\special-parameters.cbotset \
  --start='01/01/2024 12:34' --end='31/08/2024 20:56' \
  --balance=10000 --commission=30 --data-mode=m1 --spread=1 \
  --ctid=letstrade --pwd-file=C:\test\password.pwd --account=4791386


