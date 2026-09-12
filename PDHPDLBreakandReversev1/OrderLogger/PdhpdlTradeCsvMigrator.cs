using System;
using System.Globalization;

namespace cAlgo.Robots;

// Upgrades a previously written trades CSV to the current column schema. This is a separate
// concern from PdhpdlTradeCsvLogger: the logger writes today's format, this migrator knows the
// history of older layouts (fewer columns, equity columns in different positions, per-pullback
// entry-mode columns) and rewrites old rows in place. Pure string work, no cAlgo dependency.
public static class PdhpdlTradeCsvMigrator {
    private const int CurrentColumnCount = 30;
    // GapX_1Bar 一列加入之前的 schema（那时 GapX_3Bar 还叫 GapX，位置没变，只需补列）。
    private const int ColumnCountBeforeShortGapX = 29;
    // GapX 一列加入之前的 schema。
    private const int ColumnCountBeforeGapX = 28;
    // ADX14_H1_Previous 一列加入之前的 schema。
    private const int ColumnCountBeforeAdxPreviousState = 27;
    // ADX14_H1 / DI+14_H1 / DI-14_H1 三列加入之前的 schema。
    private const int ColumnCountBeforeDmsState = 24;
    // ATR_Ratio_H1 / PD_Range_ATR 两列加入之前的 schema。这两种旧行都只需在末尾补空列。
    private const int ColumnCountBeforeAtrState = 22;
    // "多空"(Side) 列移除之前的旧 schema：所有历史布局的第 1 列（索引 SideColumnIndex）都是 Side。
    private const int ColumnCountWithSide = 23;
    private const int PreviousColumnCount = 26;
    private const int OldColumnCountBeforeAccountEquity = 23;
    private const int OldColumnCountBeforeSingleTakeProfit = 24;
    private const int OldColumnCountBeforeClosePrice = 25;
    private const int SideColumnIndex = 1;

    // 移除 "多空" 列之前，本 cBot 输出的表头。用来把 "含 Side 的旧 current 布局(23 列)" 与
    // "更早、同为 23 列的 before-account-equity 布局" 区分开——这正是过去用 hasCurrentHeader 承担的判断。
    private const string PreviousHeaderWithSide =
        "编号,多空,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";

    // 加入 DMI 三列之前的表头（24 列）。它的列数与更早的 OldColumnCountBeforeSingleTakeProfit 相同，
    // 只能靠表头把两者区分开。
    private const string PreviousHeaderBeforeDmsState =
        "编号,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID,ATR_Ratio_H1,PD_Range_ATR";

    // Returns the upgraded lines (header replaced, old rows rewritten), or null when the file is
    // already on the current schema and needs no rewrite.
    public static string[] Upgrade(string[] lines, string currentHeader) {
        bool hasCurrentHeader = lines[0] == currentHeader;

        if (hasCurrentHeader && !NeedsRowMigration(lines))
            return null;

        bool isPreviousWithSideHeader = lines[0] == PreviousHeaderWithSide;
        MigrateRows(lines, isPreviousWithSideHeader, lines[0] == PreviousHeaderBeforeDmsState);
        lines[0] = currentHeader;
        return lines;
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

    private static void MigrateRows(string[] lines, bool isPreviousWithSideHeader, bool isHeaderBeforeDmsState) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (columns.Length == CurrentColumnCount)
                continue;

            // 只在末尾追加过列的两个近期 schema：补空列即可，列序没有变过。
            // 24 列同时也是更早的 OldColumnCountBeforeSingleTakeProfit 布局，所以要看表头。
            if (columns.Length == ColumnCountBeforeAtrState || columns.Length == ColumnCountBeforeAdxPreviousState ||
                columns.Length == ColumnCountBeforeGapX || columns.Length == ColumnCountBeforeShortGapX ||
                (columns.Length == ColumnCountBeforeDmsState && isHeaderBeforeDmsState)) {
                lines[i] = string.Join(",", AppendEmptyColumns(columns, CurrentColumnCount));
                continue;
            }

            string[] withSide = NormalizeToWithSideLayout(columns, isPreviousWithSideHeader);

            // 只有成功归一到 "含 Side 的 23 列布局" 才剥离 Side 列；无法识别长度的行保持原样，
            // 与旧逻辑一致（旧代码对未命中任何分支的行也不改动）。
            if (withSide.Length == ColumnCountWithSide)
                lines[i] = string.Join(",", AppendEmptyColumns(RemoveSideColumn(withSide), CurrentColumnCount));
        }
    }

    private static string[] AppendEmptyColumns(string[] columns, int targetColumnCount) {
        if (columns.Length >= targetColumnCount)
            return columns;

        string[] padded = new string[targetColumnCount];
        Array.Copy(columns, padded, columns.Length);

        for (int i = columns.Length; i < targetColumnCount; i++)
            padded[i] = "";

        return padded;
    }

    // 把任意历史布局归一到 "含 Side 的 23 列布局"，随后由 RemoveSideColumn 统一剥离 Side。
    private static string[] NormalizeToWithSideLayout(string[] columns, bool isPreviousWithSideHeader) {
        if (isPreviousWithSideHeader && columns.Length == ColumnCountWithSide)
            return columns;

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
            columns = CollapseEntryModeColumns(columns);

        return columns;
    }

    // 删除索引 SideColumnIndex 处的 "多空" 列，把 23 列布局收敛到 ColumnCountBeforeAtrState 布局。
    private static string[] RemoveSideColumn(string[] columnsWithSide) {
        string[] result = new string[columnsWithSide.Length - 1];
        result[0] = columnsWithSide[0];
        Array.Copy(columnsWithSide, SideColumnIndex + 1, result, SideColumnIndex, columnsWithSide.Length - SideColumnIndex - 1);
        return result;
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
        string[] collapsed = new string[ColumnCountWithSide];
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

    private static bool IsNumber(string value) {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
