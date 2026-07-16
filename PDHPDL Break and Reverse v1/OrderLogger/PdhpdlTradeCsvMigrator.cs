using System;
using System.Globalization;

namespace cAlgo.Robots;

// Upgrades a previously written trades CSV to the current column schema. This is a separate
// concern from PdhpdlTradeCsvLogger: the logger writes today's format, this migrator knows the
// history of older layouts (fewer columns, equity columns in different positions, per-pullback
// entry-mode columns) and rewrites old rows in place. Pure string work, no cAlgo dependency.
public static class PdhpdlTradeCsvMigrator {
    private const int CurrentColumnCount = 23;
    private const int PreviousColumnCount = 26;
    private const int OldColumnCountBeforeAccountEquity = 23;
    private const int OldColumnCountBeforeSingleTakeProfit = 24;
    private const int OldColumnCountBeforeClosePrice = 25;

    // Returns the upgraded lines (header replaced, old rows rewritten), or null when the file is
    // already on the current schema and needs no rewrite.
    public static string[] Upgrade(string[] lines, string currentHeader) {
        bool hasCurrentHeader = lines[0] == currentHeader;

        if (hasCurrentHeader && !NeedsRowMigration(lines))
            return null;

        MigrateRows(lines, hasCurrentHeader);
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

    private static bool IsNumber(string value) {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
