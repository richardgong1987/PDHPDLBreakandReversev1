using System;
using System.Globalization;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvLogger {
    private const string FileName = "pdhpdl-trades.csv";
    private const int CurrentColumnCount = 25;
    private const int OldColumnCountBeforeAccountEquity = 23;
    private const int OldColumnCountBeforeSingleTakeProfit = 24;
    private const int OldColumnCountWithAccountEquityBeforeProfitLoss = 25;
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);
    private readonly string _filePath;

    public PdhpdlTradeCsvLogger() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _filePath = Path.Combine(documentsPath, FileName);

        EnsureFileExists();
    }

    public string FilePath => _filePath;

    public string AppendEntry(PdhpdlOrderPlan plan, Position position, string symbolName, string timeFrame) {
        if (plan == null || position == null)
            return "";

        string side = plan.TradeType == TradeType.Buy ? "B" : "S";
        string keyLevel = plan.TradeType == TradeType.Buy ? "PDL" : "PDH";

        var record = new PdhpdlTradeCsvRecord {
            Id = position.Id.ToString(),
            Side = side,
            KeyLevel = keyLevel,
            Signal = "false-breakout",
            CloseEntryResult = GetEntryModeCsvValue(PdhpdlEntryMode.Close, plan.EntryMode),
            Pullback25Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback25, plan.EntryMode),
            Pullback382Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback382, plan.EntryMode),
            Pullback50Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback50, plan.EntryMode),
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = plan.AccountEquity,
            CloseAccountEquity = 0.0,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = position.StopLoss ?? plan.StopPrice,
            TakeProfitPrice = plan.TakeProfitPrice,
            RiskPrice = Math.Abs(position.EntryPrice - (position.StopLoss ?? plan.StopPrice)),
            VolumeInUnits = position.VolumeInUnits,
            PositionId = position.Id.ToString(),
            DealId = GetOpenDealId(position)
        };

        Append(record);
        return record.Id;
    }

    public string AppendPendingEntry(PdhpdlOrderPlan plan, PendingOrder order, string symbolName, string timeFrame) {
        if (plan == null || order == null)
            return "";

        string side = plan.TradeType == TradeType.Buy ? "B" : "S";
        string keyLevel = plan.TradeType == TradeType.Buy ? "PDL" : "PDH";
        string csvId = order.Id.ToString();

        var record = new PdhpdlTradeCsvRecord {
            Id = csvId,
            Side = side,
            KeyLevel = keyLevel,
            Signal = "false-breakout",
            CloseEntryResult = GetEntryModeCsvValue(PdhpdlEntryMode.Close, plan.EntryMode),
            Pullback25Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback25, plan.EntryMode),
            Pullback382Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback382, plan.EntryMode),
            Pullback50Result = GetEntryModeCsvValue(PdhpdlEntryMode.Pullback50, plan.EntryMode),
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = plan.AccountEquity,
            CloseAccountEquity = 0.0,
            EntryTime = order.SubmittedTime,
            EntryPrice = order.TargetPrice,
            StopPrice = plan.StopPrice,
            TakeProfitPrice = plan.TakeProfitPrice,
            RiskPrice = plan.RiskPrice,
            VolumeInUnits = order.VolumeInUnits,
            PendingOrderId = csvId
        };

        Append(record);
        return record.Id;
    }

    public string AppendClose(Position position, PositionCloseReason reason, string csvId, string symbolName, string timeFrame, DateTime serverTime,
        double entryAccountEquity, double closeAccountEquity) {
        if (position == null)
            return "";

        string closeReason = GetCloseReasonCode(reason);
        double resolvedEntryAccountEquity = entryAccountEquity;

        if (resolvedEntryAccountEquity <= 0.0 && closeAccountEquity > 0.0)
            resolvedEntryAccountEquity = closeAccountEquity - position.NetProfit;

        var record = new PdhpdlTradeCsvRecord {
            Id = GetCloseRecordId(csvId, reason),
            Side = position.TradeType == TradeType.Buy ? "B" : "S",
            KeyLevel = "",
            Signal = "close",
            CloseEntryResult = "",
            Pullback25Result = "",
            Pullback382Result = "",
            Pullback50Result = "",
            Comment = position.NetProfit >= 0.0 ? "盈利" : "亏损",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = resolvedEntryAccountEquity,
            CloseAccountEquity = closeAccountEquity,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = 0.0,
            TakeProfitPrice = 0.0,
            RiskPrice = 0.0,
            VolumeInUnits = position.VolumeInUnits,
            CloseReason = closeReason,
            ProfitLoss = position.NetProfit,
            CloseTime = serverTime.ToString("yyyy-MM-dd HH:mm:ss"),
            PositionId = position.Id.ToString(),
            DealId = GetCloseDealId(position)
        };

        Append(record);
        return record.Id;
    }

    public void Append(PdhpdlTradeCsvRecord record) {
        if (record == null)
            return;

        string line = string.Join(",", Escape(record.Id), Escape(record.Side), Escape(record.KeyLevel), Escape(record.Signal),
            Escape(record.CloseEntryResult), Escape(record.Pullback25Result), Escape(record.Pullback382Result),
            Escape(record.Pullback50Result), Escape(record.Comment), Escape(record.Symbol), Escape(record.TimeFrame),
            Escape(record.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Escape(record.EntryPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(record.StopPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(record.TakeProfitPrice.ToString(CultureInfo.InvariantCulture)), Escape(record.RiskPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(record.VolumeInUnits.ToString(CultureInfo.InvariantCulture)), Escape(record.CloseReason),
            Escape(FormatOptionalNumber(record.EntryAccountEquity)), Escape(FormatOptionalNumber(record.CloseAccountEquity)),
            Escape(record.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(record.CloseTime), Escape(record.PendingOrderId), Escape(record.PositionId),
            Escape(record.DealId));
        System.IO.File.AppendAllText(_filePath, line + Environment.NewLine, CsvEncoding);
    }

    private void EnsureFileExists() {
        string header = BuildHeader();

        if (!System.IO.File.Exists(_filePath)) {
            System.IO.File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        string[] lines = System.IO.File.ReadAllLines(_filePath);

        if (lines.Length == 0) {
            System.IO.File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        bool hasCurrentHeader = lines[0] == header;

        if (hasCurrentHeader && !NeedsRowMigration(lines))
            return;

        lines[0] = header;
        MigrateRows(lines);
        System.IO.File.WriteAllLines(_filePath, lines, CsvEncoding);
    }

    private static string BuildHeader() {
        return string.Join(",", "编号", "多空", "关键位", "信号", "收线入场", "回撤25入场", "回撤38.2入场", "回撤50入场", "备注", "交易品种", "时间周期", "入场时间", "入场价格",
            "止损价格", "止盈价格", "风险价格距离", "下单数量", "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "挂单ID", "持仓ID", "成交ID");
    }

    private static bool NeedsRowMigration(string[] lines) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (columns.Length != CurrentColumnCount)
                return true;

            if (IsOldAccountEquityColumnOrder(columns))
                return true;

            if (IsOldNoEquityCurrentColumnOrder(columns))
                return true;
        }

        return false;
    }

    private static void MigrateRows(string[] lines) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (columns.Length == OldColumnCountBeforeAccountEquity) {
                lines[i] = string.Join(",", MigrateOldSingleTakeProfitRow(columns));
                continue;
            }

            if (columns.Length == OldColumnCountBeforeSingleTakeProfit) {
                lines[i] = string.Join(",", MigrateOldTwoTakeProfitRow(columns));
                continue;
            }

            if (columns.Length == OldColumnCountWithAccountEquityBeforeProfitLoss && IsOldAccountEquityColumnOrder(columns)) {
                MoveEquityColumnsNearProfitLoss(columns);
                lines[i] = string.Join(",", columns);
                continue;
            }

            if (columns.Length == CurrentColumnCount && IsOldNoEquityCurrentColumnOrder(columns)) {
                MoveProfitLossFromEquityColumn(columns);
                lines[i] = string.Join(",", columns);
            }
        }
    }

    private static bool IsOldAccountEquityColumnOrder(string[] columns) {
        return columns.Length == CurrentColumnCount && !LooksLikeDateTime(columns[11]) && LooksLikeDateTime(columns[13]);
    }

    private static bool IsOldNoEquityCurrentColumnOrder(string[] columns) {
        return columns.Length == CurrentColumnCount && string.IsNullOrWhiteSpace(columns[19]) && string.IsNullOrWhiteSpace(columns[20]) &&
               IsNumber(columns[18]);
    }

    private static bool LooksLikeDateTime(string value) {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static void MoveEquityColumnsNearProfitLoss(string[] columns) {
        string entryAccountEquity = columns[11];
        string closeAccountEquity = columns[12];

        for (int i = 11; i <= 18; i++)
            columns[i] = columns[i + 2];

        columns[19] = entryAccountEquity;
        columns[20] = closeAccountEquity;
    }

    private static void MoveProfitLossFromEquityColumn(string[] columns) {
        columns[20] = columns[18];
        columns[18] = "";
        columns[19] = "";
    }

    private static string[] MigrateOldSingleTakeProfitRow(string[] columns) {
        string[] migrated = CreateEmptyRow();
        Array.Copy(columns, 0, migrated, 0, 18);
        migrated[18] = "";
        migrated[19] = "";
        Array.Copy(columns, 18, migrated, 20, columns.Length - 18);
        return migrated;
    }

    private static string[] MigrateOldTwoTakeProfitRow(string[] columns) {
        string[] migrated = CreateEmptyRow();
        Array.Copy(columns, 0, migrated, 0, 15);
        migrated[15] = columns[16];
        migrated[16] = columns[17];
        migrated[17] = columns[18];
        migrated[18] = "";
        migrated[19] = "";
        Array.Copy(columns, 19, migrated, 20, columns.Length - 19);
        return migrated;
    }

    private static string[] CreateEmptyRow() {
        string[] columns = new string[CurrentColumnCount];

        for (int i = 0; i < columns.Length; i++)
            columns[i] = "";

        return columns;
    }

    private static string FormatOptionalNumber(double value) {
        if (value <= 0.0)
            return "";

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsNumber(string value) {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static string Escape(string value) {
        if (value == null)
            return string.Empty;

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        if (!mustQuote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string GetCloseReasonCode(PositionCloseReason reason) {
        switch (reason) {
            case PositionCloseReason.StopLoss:
                return "SL";
            case PositionCloseReason.StopOut:
                return "SO";
            case PositionCloseReason.TakeProfit:
                return "TP";
            default:
                return "CLOSE";
        }
    }

    private static string GetCloseRecordId(string csvId, PositionCloseReason reason) {
        if (reason == PositionCloseReason.TakeProfit)
            return $"{csvId}-TP";

        return $"{csvId}-{GetCloseReasonCode(reason)}";
    }

    private static string GetOpenDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[0].Id.ToString();
    }

    private static string GetCloseDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[position.Deals.Count - 1].Id.ToString();
    }

    private static string GetEntryModeCsvValue(PdhpdlEntryMode columnMode, PdhpdlEntryMode selectedMode) {
        return columnMode == selectedMode ? "ORDER" : "";
    }
}
