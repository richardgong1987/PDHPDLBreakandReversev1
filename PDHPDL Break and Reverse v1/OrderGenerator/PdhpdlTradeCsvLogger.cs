using System;
using System.Globalization;
using System.IO;
using System.Text;

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
            Escape(record.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(record.CloseTime), Escape(record.CtraderId));
        File.AppendAllText(_filePath, line + Environment.NewLine, CsvEncoding);
    }

    private void EnsureFileExists() {
        string header = BuildHeader();

        if (!File.Exists(_filePath)) {
            File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        string[] lines = File.ReadAllLines(_filePath);

        if (lines.Length == 0) {
            File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        if (lines[0] == header)
            return;

        lines[0] = header;
        File.WriteAllLines(_filePath, lines, CsvEncoding);
    }

    private static string BuildHeader() {
        return string.Join(",", "编号", "多空", "关键位", "信号", "收线入场", "回撤25入场", "回撤38.2入场", "回撤50入场", "备注", "交易品种", "时间周期", "入场时间", "入场价格",
            "止损价格", "第一止盈价格", "第二止盈价格", "风险价格距离", "下单数量", "平仓原因", "平仓盈亏", "平仓时间", "cTrader平台ID");
    }

    private static string Escape(string value) {
        if (value == null)
            return string.Empty;

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        if (!mustQuote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
