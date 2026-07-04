namespace cAlgo.Robots;

public static class RiskUtil {
    // Risk money is the currency you accept losing on one trade.
    // Example: equity 10000 at 1% risk = 100; equity 12345 at 1% = 123.45.
    public static double CalculateRiskMoney(double equity, double riskPct) {
        if (equity <= 0.0 || riskPct <= 0.0)
            return 0.0;

        return equity * riskPct / 100.0;
    }
}
