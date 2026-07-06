env CTRADER_CLI_AUTHTOKEN=knS14gR_Zq2rqoes-NVEw9gvmOaj1fZ4m8AjsN5cgrw \
"/Applications/cTrader.app/Contents/MacOS/cTrader.Mac" \
run "/Users/hanjingong/cAlgo/Sources/Robots/PDHPDL Break and Reverse v1.algo" \
--ctid="richardgong1988@gmail.com" --account="5846740" \
--symbol="XAUUSD" --period="h1" --port="5034" --environment-variables --full-access \
--LineThickness="3" --ResetTradeLogOnStart="True" \
--RiskPct="1" --RiskSafetyFactor="1" \
--StopOffsetTicks="15" --MinRiskPrice="5" \
--TakeProfitR="2" --EntryModel="0" \
--NoNewOrdersStartHour="4" --ForceCloseHour="4" \
--ForceCloseMinute="30" --ResumeTradingHour="8" \
--FridayNoNewOrdersStartHour="0" --FridayForceCloseHour="3" \
--FridayForceCloseMinute="30" --ShowDebugLogs="False" --IsDebug="False" --FileName="pdhpdl-trades.csv"

