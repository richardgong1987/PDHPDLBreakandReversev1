using System;
using System.Collections.Generic;
using System.Globalization;

namespace cAlgo.Robots;

public class PdhpdlRiskGuard {
    private readonly PdhpdlRiskGuardConfigModel _configModel;
    private readonly List<NewsBlackoutWindowModel> _newsBlackoutWindows;

    public PdhpdlRiskGuard(PdhpdlRiskGuardConfigModel configModel) {
        _configModel = configModel ?? new PdhpdlRiskGuardConfigModel();
        _newsBlackoutWindows = ParseNewsBlackoutWindows(_configModel.NewsBlackoutWindows);
    }

    public int NewsBlackoutWindowCount {
        get { return _newsBlackoutWindows.Count; }
    }

    public bool ShouldBlockNewOrder(DateTime time) {
        if (IsInNewsBlackout(time))
            return true;

        if (time.DayOfWeek == DayOfWeek.Sunday)
            return true;

        return IsSaturdayForceCloseTime(time);
    }

    public bool ShouldForceClose(DateTime time) {
        if (IsInNewsBlackout(time))
            return true;

        return IsSaturdayForceCloseTime(time);
    }

    public bool TryGetStopLossPipsRejectReason(double stopLossPips, out string rejectReason) {
        rejectReason = "";

        if (stopLossPips <= 0.0) {
            rejectReason = "Stop loss pips is not positive.";
            return true;
        }

        if (_configModel.MinStopLossPips > 0.0 && stopLossPips < _configModel.MinStopLossPips) {
            rejectReason = $"Stop loss distance is too small. StopLossPips={stopLossPips}, MinStopLossPips={_configModel.MinStopLossPips}";
            return true;
        }

        return false;
    }

    // Risk money is the account currency you accept losing on one trade: a percentage of
    // equity (e.g. 1% of 10000 = 100), scaled by the safety factor. Non-positive inputs risk 0.
    public double CalculateRiskMoney(double equity, double riskPct) {
        if (equity <= 0.0 || riskPct <= 0.0)
            return 0.0;

        double proportionalRiskMoney = equity * riskPct / 100.0;
        double safetyFactor = Math.Max(0.1, Math.Min(_configModel.RiskSafetyFactor, 1.0));
        return proportionalRiskMoney * safetyFactor;
    }

    public bool IsInNewsBlackout(DateTime time) {
        foreach (NewsBlackoutWindowModel window in _newsBlackoutWindows) {
            if (time >= window.Start && time < window.End)
                return true;
        }

        return false;
    }

    private bool IsSaturdayForceCloseTime(DateTime time) {
        if (time.DayOfWeek != DayOfWeek.Saturday)
            return false;

        int forceCloseMinutes = _configModel.SaturdayForceCloseHour * 60 + _configModel.SaturdayForceCloseMinute;
        return GetMinutesOfDay(time) >= forceCloseMinutes;
    }

    private static int GetMinutesOfDay(DateTime time) {
        return time.Hour * 60 + time.Minute;
    }

    private static List<NewsBlackoutWindowModel> ParseNewsBlackoutWindows(string value) {
        var windows = new List<NewsBlackoutWindowModel>();

        if (string.IsNullOrWhiteSpace(value))
            return windows;

        string[] entries = value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string entry in entries) {
            string[] range = entry.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);

            if (range.Length != 2)
                continue;

            if (!TryParseBlackoutTime(range[0], out DateTime start))
                continue;

            if (!TryParseBlackoutTime(range[1], out DateTime end))
                continue;

            if (end <= start)
                continue;

            windows.Add(new NewsBlackoutWindowModel { Start = start, End = end });
        }

        return windows;
    }

    private static bool TryParseBlackoutTime(string value, out DateTime time) {
        string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss" };

        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
