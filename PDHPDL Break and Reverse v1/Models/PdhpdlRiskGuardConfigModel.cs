namespace cAlgo.Robots;

public class PdhpdlRiskGuardConfigModel {
    public double RiskSafetyFactor { get; set; }

    public double MinRiskPrice { get; set; }

    public int FridayNoNewOrdersStartHour { get; set; }

    public int SaturdayForceCloseHour { get; set; }

    public int SaturdayForceCloseMinute { get; set; }

    public string NewsBlackoutWindows { get; set; } = "";
}
