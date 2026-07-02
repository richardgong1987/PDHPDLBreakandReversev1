namespace cAlgo.Robots;

public class PdhpdlRiskGuardConfig {
    public double RiskSafetyFactor { get; set; }

    public double MinRiskPrice { get; set; }

    public int NoNewOrdersStartHour { get; set; }

    public int ForceCloseHour { get; set; }

    public int ForceCloseMinute { get; set; }

    public int ResumeTradingHour { get; set; }

    public int FridayNoNewOrdersStartHour { get; set; }

    public int FridayForceCloseHour { get; set; }

    public int FridayForceCloseMinute { get; set; }

    public string NewsBlackoutWindows { get; set; } = "";
}
