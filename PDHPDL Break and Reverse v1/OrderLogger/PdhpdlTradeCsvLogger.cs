using System;
using System.Globalization;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvLogger {
    private const int CurrentColumnCount = 23;
    private const int PreviousColumnCount = 26;
    private const int OldColumnCountBeforeAccountEquity = 23;
    private const int OldColumnCountBeforeSingleTakeProfit = 24;
    private const int OldColumnCountBeforeClosePrice = 25;
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);
    private readonly string _filePath;

    // resetOnStart: overwrite the file with a fresh header so it holds only this run.
    // The file is fixed and append-only, so without this every backtest run stacks another
    // copy of the same trades. Turn it off to keep history across runs.
    public PdhpdlTradeCsvLogger(bool resetOnStart, string fileName) {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _filePath = Path.Combine(documentsPath, fileName);

        if (resetOnStart)
            System.IO.File.WriteAllText(_filePath, BuildHeader() + Environment.NewLine, CsvEncoding);
        else
            EnsureFileExists();
    }

    public string FilePath => _filePath;

    public string AppendEntry(PdhpdlOrderPlanModel planModel, Position position, string symbolName, string timeFrame) {
        if (planModel == null || position == null)
            return "";

        string side = planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "B" : "S";
        string keyLevel = planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "PDL" : "PDH";

        var record = new PdhpdlTradeCsvRecordModel {
            Id = position.Id.ToString(),
            Side = side,
            KeyLevel = keyLevel,
            Signal = planModel.SignalName,
            EntryMode = GetEntryModeCsvValue(planModel.EntryModel),
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = planModel.AccountEquity,
            CloseAccountEquity = 0.0,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = position.StopLoss ?? planModel.StopPrice,
            TakeProfitPrice = planModel.TakeProfitPrice,
            RiskPrice = Math.Abs(position.EntryPrice - (position.StopLoss ?? planModel.StopPrice)),
            VolumeInUnits = position.VolumeInUnits,
            PositionId = position.Id.ToString(),
            DealId = GetOpenDealId(position)
        };

        Append(record);
        return record.Id;
    }

    public string AppendPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order, string symbolName, string timeFrame) {
        if (planModel == null || order == null)
            return "";

        string side = planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "B" : "S";
        string keyLevel = planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "PDL" : "PDH";
        string csvId = order.Id.ToString();

        var record = new PdhpdlTradeCsvRecordModel {
            Id = csvId,
            Side = side,
            KeyLevel = keyLevel,
            Signal = "false-breakout",
            EntryMode = GetEntryModeCsvValue(planModel.EntryModel),
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = planModel.AccountEquity,
            CloseAccountEquity = 0.0,
            EntryTime = order.SubmittedTime,
            EntryPrice = order.TargetPrice,
            StopPrice = planModel.StopPrice,
            TakeProfitPrice = planModel.TakeProfitPrice,
            RiskPrice = planModel.RiskPrice,
            VolumeInUnits = order.VolumeInUnits,
            PendingOrderId = csvId
        };

        Append(record);
        return record.Id;
    }

    public string AppendClose(Position position, PositionCloseReason reason, string csvId, string symbolName, string timeFrame,
        DateTime serverTime, double closePrice, double entryAccountEquity, double closeAccountEquity) {
        if (position == null)
            return "";

        string closeReason = GetCloseReasonCode(reason);
        double resolvedEntryAccountEquity = entryAccountEquity;

        if (resolvedEntryAccountEquity <= 0.0 && closeAccountEquity > 0.0)
            resolvedEntryAccountEquity = closeAccountEquity - position.NetProfit;

        var record = new PdhpdlTradeCsvRecordModel {
            Id = GetCloseRecordId(csvId, reason),
            Side = position.TradeType == TradeType.Buy ? "B" : "S",
            KeyLevel = "",
            Signal = "close",
            EntryMode = "",
            Comment = position.NetProfit >= 0.0 ? "盈利" : "亏损",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = resolvedEntryAccountEquity,
            CloseAccountEquity = closeAccountEquity,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            ClosePrice = closePrice,
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

    public void Append(PdhpdlTradeCsvRecordModel recordModel) {
        if (recordModel == null)
            return;

        string line = string.Join(",", Escape(recordModel.Id), Escape(recordModel.Side), Escape(recordModel.KeyLevel),
            Escape(recordModel.Signal), Escape(recordModel.EntryMode), Escape(recordModel.Comment), Escape(recordModel.Symbol),
            Escape(recordModel.TimeFrame),
            Escape(recordModel.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Escape(recordModel.EntryPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(FormatOptionalNumber(recordModel.ClosePrice)),
            Escape(recordModel.StopPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.TakeProfitPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.RiskPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.VolumeInUnits.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseReason),
            Escape(FormatOptionalNumber(recordModel.EntryAccountEquity)), Escape(FormatOptionalNumber(recordModel.CloseAccountEquity)),
            Escape(recordModel.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseTime),
            Escape(recordModel.PendingOrderId), Escape(recordModel.PositionId), Escape(recordModel.DealId));
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

        MigrateRows(lines, hasCurrentHeader);
        lines[0] = header;
        System.IO.File.WriteAllLines(_filePath, lines, CsvEncoding);
    }

    private static string BuildHeader() {
        return string.Join(",", "编号", "多空", "关键位", "信号", "回撤开仓模式", "备注", "交易品种", "时间周期", "入场时间", "入场价格",
            "平仓价格", "止损价格", "止盈价格", "风险价格距离", "下单数量", "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "挂单ID", "持仓ID", "成交ID");
    }

    private static bool NeedsRowMigration(string[] lines) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (columns.Length != CurrentColumnCount)
                return true;
        }

        return false;
    }

    private static void MigrateRows(string[] lines, bool hasCurrentHeader) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (hasCurrentHeader && columns.Length == CurrentColumnCount)
                continue;

            if (columns.Length == OldColumnCountBeforeAccountEquity)
                columns = MigrateOldSingleTakeProfitRow(columns);

            else if (columns.Length == OldColumnCountBeforeSingleTakeProfit)
                columns = MigrateOldTwoTakeProfitRow(columns);

            else if (columns.Length == OldColumnCountBeforeClosePrice) {
                if (IsOldAccountEquityColumnOrder(columns))
                    MoveEquityColumnsNearProfitLoss(columns);

                if (IsOldNoEquityCurrentColumnOrder(columns))
                    MoveProfitLossFromEquityColumn(columns);

                columns = MigrateOldRowBeforeClosePrice(columns);
            }

            if (columns.Length == PreviousColumnCount)
                lines[i] = string.Join(",", CollapseEntryModeColumns(columns));
        }
    }

    private static bool IsOldAccountEquityColumnOrder(string[] columns) {
        return columns.Length == OldColumnCountBeforeClosePrice && !LooksLikeDateTime(columns[11]) && LooksLikeDateTime(columns[13]);
    }

    private static bool IsOldNoEquityCurrentColumnOrder(string[] columns) {
        return columns.Length == OldColumnCountBeforeClosePrice && string.IsNullOrWhiteSpace(columns[19]) &&
               string.IsNullOrWhiteSpace(columns[20]) && IsNumber(columns[18]);
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
        string[] migrated = CreateEmptyPreviousRow();
        Array.Copy(columns, 0, migrated, 0, 13);
        Array.Copy(columns, 13, migrated, 14, 5);
        Array.Copy(columns, 18, migrated, 21, columns.Length - 18);
        return migrated;
    }

    private static string[] MigrateOldTwoTakeProfitRow(string[] columns) {
        string[] migrated = CreateEmptyPreviousRow();
        Array.Copy(columns, 0, migrated, 0, 13);
        migrated[14] = columns[13];
        migrated[15] = columns[14];
        migrated[16] = columns[16];
        migrated[17] = columns[17];
        migrated[18] = columns[18];
        Array.Copy(columns, 19, migrated, 21, columns.Length - 19);
        return migrated;
    }

    private static string[] MigrateOldRowBeforeClosePrice(string[] columns) {
        string[] migrated = CreateEmptyPreviousRow();
        Array.Copy(columns, 0, migrated, 0, 13);
        Array.Copy(columns, 13, migrated, 14, columns.Length - 13);
        return migrated;
    }

    private static string[] CreateEmptyPreviousRow() {
        string[] columns = new string[PreviousColumnCount];

        for (int i = 0; i < columns.Length; i++)
            columns[i] = "";

        return columns;
    }

    private static string[] CollapseEntryModeColumns(string[] columns) {
        string[] collapsed = new string[CurrentColumnCount];
        Array.Copy(columns, 0, collapsed, 0, 4);
        collapsed[4] = GetLegacyEntryMode(columns);
        Array.Copy(columns, 8, collapsed, 5, columns.Length - 8);
        return collapsed;
    }

    private static string GetLegacyEntryMode(string[] columns) {
        if (!string.IsNullOrWhiteSpace(columns[4]))
            return "收线入场";

        if (!string.IsNullOrWhiteSpace(columns[5]))
            return "回撤25入场";

        if (!string.IsNullOrWhiteSpace(columns[6]))
            return "回撤38.2入场";

        if (!string.IsNullOrWhiteSpace(columns[7]))
            return "回撤50入场";

        return "";
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

    private static string GetEntryModeCsvValue(PdhpdlEntryModel entryModel) {
        switch (entryModel) {
            case PdhpdlEntryModel.Pb25:
                return "回撤25入场";
            case PdhpdlEntryModel.Pb382:
                return "回撤38.2入场";
            case PdhpdlEntryModel.Pb50:
                return "回撤50入场";
            default:
                return "收线入场";
        }
    }
}
