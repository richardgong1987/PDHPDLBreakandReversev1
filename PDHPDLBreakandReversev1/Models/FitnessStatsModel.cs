namespace cAlgo.Robots;

// The pass-level statistics cTrader's built-in fitness is built from, copied out of
// GetFitnessArgs so AnnualFitness stays independent of cAlgo.API.
public class FitnessStatsModel {
    public double NetProfit { get; set; }

    public double WinningTrades { get; set; }

    // 0-100, not 0-1: a 40% drawdown arrives as 40.
    public double MaxEquityDrawdownPercent { get; set; }
}
