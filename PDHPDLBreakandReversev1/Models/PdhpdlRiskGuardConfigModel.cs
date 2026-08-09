namespace cAlgo.Robots;

public class PdhpdlRiskGuardConfigModel {
    public double RiskSafetyFactor { get; set; }

    public double MinStopLossPips { get; set; }

    public int SaturdayForceCloseHour { get; set; }

    public int SaturdayForceCloseMinute { get; set; }

    public string NewsBlackoutWindows { get; set; } = "";
}
