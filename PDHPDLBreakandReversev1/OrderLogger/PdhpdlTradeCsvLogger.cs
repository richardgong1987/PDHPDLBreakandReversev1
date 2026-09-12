using System;
using System.Globalization;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class PdhpdlTradeCsvLogger {
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);
    private readonly string _filePath;

    // resetOnStart: overwrite the file with a fresh header so it holds only this run.
    // The file is fixed and append-only, so without this every backtest run stacks another
    // copy of the same trades. Turn it off to keep history across runs.
    //
    // reportsDirectory: 按运行模式选定的输出目录（回测/模拟/实盘各一个，见组合根）。
    // fileName 传绝对路径时（回测经 run_conditions 指定完整路径）直接采用、忽略 reportsDirectory。
    public PdhpdlTradeCsvLogger(bool resetOnStart, string reportsDirectory, string fileName) {
        _filePath = ResolveFilePath(reportsDirectory, fileName);
        EnsureDirectoryExists(_filePath);

        if (resetOnStart)
            System.IO.File.WriteAllText(_filePath, BuildHeader() + Environment.NewLine, CsvEncoding);
        else
            EnsureFileExists();
    }

    private static string ResolveFilePath(string reportsDirectory, string fileName) {
        if (Path.IsPathRooted(fileName))
            return fileName;

        return Path.Combine(reportsDirectory, fileName);
    }

    private static void EnsureDirectoryExists(string filePath) {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public string FilePath => _filePath;

    public string AppendEntry(PdhpdlOrderPlanModel planModel, Position position, string symbolName, string timeFrame) {
        if (planModel == null || position == null)
            return "";

        var record = new PdhpdlTradeCsvRecordModel {
            Id = position.Id.ToString(),
            KeyLevel = planModel.KeyLevel,
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
            DealId = GetOpenDealId(position),
            AtrRatioH1 = planModel.AtrRatioH1,
            PdRangeAtr = planModel.PdRangeAtr,
            Adx14H1 = planModel.Adx14H1,
            Adx14H1Previous = planModel.Adx14H1Previous,
            GapExpansionX3Bar = planModel.GapExpansionX3Bar,
            GapExpansionX1Bar = planModel.GapExpansionX1Bar,
            DiPlus14H1 = planModel.DiPlus14H1,
            DiMinus14H1 = planModel.DiMinus14H1
        };

        Append(record);
        return record.Id;
    }

    public string AppendPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order, string symbolName, string timeFrame) {
        if (planModel == null || order == null)
            return "";

        string keyLevel = planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "PDL" : "PDH";
        string csvId = order.Id.ToString();

        var record = new PdhpdlTradeCsvRecordModel {
            Id = csvId,
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
            PendingOrderId = csvId,
            AtrRatioH1 = planModel.AtrRatioH1,
            PdRangeAtr = planModel.PdRangeAtr,
            Adx14H1 = planModel.Adx14H1,
            Adx14H1Previous = planModel.Adx14H1Previous,
            GapExpansionX3Bar = planModel.GapExpansionX3Bar,
            GapExpansionX1Bar = planModel.GapExpansionX1Bar,
            DiPlus14H1 = planModel.DiPlus14H1,
            DiMinus14H1 = planModel.DiMinus14H1
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

        string line = string.Join(",", Escape(recordModel.Id), Escape(recordModel.KeyLevel), Escape(recordModel.Signal),
            Escape(recordModel.EntryMode), Escape(recordModel.Comment), Escape(recordModel.Symbol), Escape(recordModel.TimeFrame),
            Escape(recordModel.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Escape(recordModel.EntryPrice.ToString(CultureInfo.InvariantCulture)), Escape(FormatOptionalNumber(recordModel.ClosePrice)),
            Escape(recordModel.StopPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.TakeProfitPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.RiskPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.VolumeInUnits.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseReason),
            Escape(FormatOptionalNumber(recordModel.EntryAccountEquity)), Escape(FormatOptionalNumber(recordModel.CloseAccountEquity)),
            Escape(recordModel.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseTime),
            Escape(recordModel.PendingOrderId), Escape(recordModel.PositionId), Escape(recordModel.DealId),
            Escape(FormatMetric(recordModel.AtrRatioH1)), Escape(FormatMetric(recordModel.PdRangeAtr)),
            Escape(FormatMetric(recordModel.Adx14H1)), Escape(FormatMetric(recordModel.DiPlus14H1)),
            Escape(FormatMetric(recordModel.DiMinus14H1)), Escape(FormatMetric(recordModel.Adx14H1Previous)),
            Escape(FormatMetric(recordModel.GapExpansionX3Bar)), Escape(FormatMetric(recordModel.GapExpansionX1Bar)));
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

        string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, header);

        if (upgraded != null)
            System.IO.File.WriteAllLines(_filePath, upgraded, CsvEncoding);
    }

    private static string BuildHeader() {
        // "多空" (Side) 字段已废弃，从当前表头中移除。历史文件由 PdhpdlTradeCsvMigrator 升级时会剥离该列。
        return string.Join(",", "编号", "关键位", "信号", "回撤开仓模式", "备注", "交易品种", "时间周期", "入场时间", "入场价格", "平仓价格", "止损价格", "止盈价格", "风险价格距离", "下单数量",
            "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "挂单ID", "持仓ID", "成交ID", "ATR_Ratio_H1", "PD_Range_ATR",
            "ADX14_H1", "DI+14_H1", "DI-14_H1", "ADX14_H1_Previous", "GapX_3Bar", "GapX_1Bar");
    }

    // 波动/趋势状态值只写在开仓行；平仓行与数据不足（暖机期）的情况留空。
    // 只有 NaN 算缺失：0 是 DI 的合法取值（单边行情里另一侧可以归零），负数是 GapX 的合法取值（开口在收窄）。
    private static string FormatMetric(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "";

        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatOptionalNumber(double value) {
        if (value <= 0.0)
            return "";

        return value.ToString(CultureInfo.InvariantCulture);
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
