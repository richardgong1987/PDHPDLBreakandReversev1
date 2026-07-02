using System;
using System.Globalization;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvLogger {
    private const string FileName = "pdhpdl-trades.csv";
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
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = position.StopLoss ?? plan.StopPrice,
            Tp1Price = plan.Tp1Price,
            Tp2Price = plan.Tp2Price,
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
            EntryTime = order.SubmittedTime,
            EntryPrice = order.TargetPrice,
            StopPrice = plan.StopPrice,
            Tp1Price = plan.Tp1Price,
            Tp2Price = plan.Tp2Price,
            RiskPrice = plan.RiskPrice,
            VolumeInUnits = order.VolumeInUnits,
            PendingOrderId = csvId
        };

        Append(record);
        return record.Id;
    }

    public string AppendClose(Position position, PositionCloseReason reason, string csvId, string symbolName, string timeFrame, DateTime serverTime) {
        if (position == null)
            return "";

        string closeReason = GetCloseReasonCode(reason);

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
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = 0.0,
            Tp1Price = 0.0,
            Tp2Price = 0.0,
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

    public string AppendTp1(Position position, double closeVolumeInUnits, string csvId, string symbolName, string timeFrame, DateTime serverTime) {
        if (position == null)
            return "";

        var record = new PdhpdlTradeCsvRecord {
            Id = $"{csvId}-TP1",
            Side = position.TradeType == TradeType.Buy ? "B" : "S",
            KeyLevel = "",
            Signal = "partial-close",
            CloseEntryResult = "",
            Pullback25Result = "",
            Pullback382Result = "",
            Pullback50Result = "",
            Comment = "TP1部分止盈",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = 0.0,
            Tp1Price = 0.0,
            Tp2Price = 0.0,
            RiskPrice = 0.0,
            VolumeInUnits = closeVolumeInUnits,
            CloseReason = "TP1",
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
            Escape(record.StopPrice.ToString(CultureInfo.InvariantCulture)), Escape(record.Tp1Price.ToString(CultureInfo.InvariantCulture)),
            Escape(record.Tp2Price.ToString(CultureInfo.InvariantCulture)), Escape(record.RiskPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(record.VolumeInUnits.ToString(CultureInfo.InvariantCulture)), Escape(record.CloseReason),
            Escape(record.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(record.CloseTime), Escape(record.PendingOrderId),
            Escape(record.PositionId), Escape(record.DealId));
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

        if (lines[0] == header)
            return;

        lines[0] = header;
        System.IO.File.WriteAllLines(_filePath, lines, CsvEncoding);
    }

    private static string BuildHeader() {
        return string.Join(",", "编号", "多空", "关键位", "信号", "收线入场", "回撤25入场", "回撤38.2入场", "回撤50入场", "备注", "交易品种", "时间周期", "入场时间", "入场价格",
            "止损价格", "第一止盈价格", "第二止盈价格", "风险价格距离", "下单数量", "平仓原因", "平仓盈亏", "平仓时间", "挂单ID", "持仓ID", "成交ID");
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
            return $"{csvId}-TP2";

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
