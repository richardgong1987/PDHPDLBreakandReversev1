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

        if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
            return true;

        if (IsFridayNoNewOrderTime(time))
            return true;

        return IsInNoNewOrderWindow(time);
    }

    public bool ShouldForceClose(DateTime time) {
        if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
            return true;

        if (IsFridayForceCloseTime(time))
            return true;

        if (IsInNewsBlackout(time))
            return true;

        return IsInForceCloseWindow(time);
    }

    public bool TryGetRiskPriceRejectReason(double riskPrice, out string rejectReason) {
        rejectReason = "";

        if (riskPrice <= 0.0) {
            rejectReason = "Risk price is not positive.";
            return true;
        }

        if (_configModel.MinRiskPrice > 0.0 && riskPrice < _configModel.MinRiskPrice) {
            rejectReason = $"Risk price is too small. RiskPrice={riskPrice}, MinRiskPrice={_configModel.MinRiskPrice}";
            return true;
        }

        return false;
    }

    public double CalculateRiskMoney(double equity, double riskPct) {
        double proportionalRiskMoney = RiskUtil.CalculateRiskMoney(equity, riskPct);
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

    private bool IsFridayNoNewOrderTime(DateTime time) {
        if (time.DayOfWeek != DayOfWeek.Friday)
            return false;

        return GetMinutesOfDay(time) >= _configModel.FridayNoNewOrdersStartHour * 60;
    }

    private bool IsFridayForceCloseTime(DateTime time) {
        if (time.DayOfWeek != DayOfWeek.Friday)
            return false;

        return GetMinutesOfDay(time) >= _configModel.FridayForceCloseHour * 60 + _configModel.FridayForceCloseMinute;
    }

    private bool IsInNoNewOrderWindow(DateTime time) {
        int currentMinutes = GetMinutesOfDay(time);
        int startMinutes = _configModel.NoNewOrdersStartHour * 60;
        int resumeMinutes = _configModel.ResumeTradingHour * 60;

        return IsWithinWindow(currentMinutes, startMinutes, resumeMinutes);
    }

    private bool IsInForceCloseWindow(DateTime time) {
        int currentMinutes = GetMinutesOfDay(time);
        int startMinutes = _configModel.ForceCloseHour * 60 + _configModel.ForceCloseMinute;
        int resumeMinutes = _configModel.ResumeTradingHour * 60;

        return IsWithinWindow(currentMinutes, startMinutes, resumeMinutes);
    }

    private static int GetMinutesOfDay(DateTime time) {
        return time.Hour * 60 + time.Minute;
    }

    private static bool IsWithinWindow(int currentMinutes, int startMinutes, int endMinutes) {
        if (startMinutes == endMinutes)
            return true;

        if (startMinutes < endMinutes)
            return currentMinutes >= startMinutes && currentMinutes < endMinutes;

        return currentMinutes >= startMinutes || currentMinutes < endMinutes;
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
