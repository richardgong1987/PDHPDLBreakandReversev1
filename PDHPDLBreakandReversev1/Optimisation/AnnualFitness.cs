using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Robots;

// Custom optimisation fitness: keep only the passes that made money in *every* calendar year of
// the optimisation window. One monster year carrying four flat ones is a curve fit, not an edge,
// and the platform's own score cannot tell the two apart.
//
// A year inside the window with no trades at all counts as a failed year: it earned nothing.
// That is why the window comes from the robot's clock rather than from the trades themselves —
// idle years leave no trace in the history.
//
// Surviving passes keep cTrader's built-in score, so the ranking among them is exactly the one
// the platform would have produced:
//
//     net profit x winning trades / (1 + max equity drawdown% / 100)
//
// Rejected passes always score below survivors, because a survivor's score is positive by
// construction (profitable every year means NetProfit > 0 and at least one winning trade). They
// are scored by how much their bad years lost, so the optimiser's genetic search still sees a
// gradient towards the feasible region instead of a flat wall of identical rejections.
public class AnnualFitness {
    // A pass that never traded has no gradient to offer; park it below every scored rejection.
    // Well under any summed yearly loss expressed in deposit currency.
    private const double NoTradesFitness = -1e12;

    // Charged per failed year so that a break-even or idle year (profit exactly 0) still scores
    // strictly below a survivor instead of tying with a flawless pass at zero.
    private const double FailedYearPenalty = 1d;

    private readonly int _firstYear;
    private readonly int _lastYear;

    public AnnualFitness(DateTime windowStart, DateTime windowEnd) {
        _firstYear = windowStart.Year;
        _lastYear = windowEnd.Year;
    }

    public double Calculate(IEnumerable<ClosedTradeModel> closedTrades, FitnessStatsModel stats) {
        Dictionary<int, double> profitByYear = SumProfitByYear(closedTrades);
        if (profitByYear.Count == 0)
            return NoTradesFitness;

        double[] yearlyProfits = ProfitOfEveryYearInWindow(profitByYear);
        if (yearlyProfits.All(profit => profit > 0))
            return BuiltInScore(stats);

        return RejectionScore(yearlyProfits);
    }

    private static Dictionary<int, double> SumProfitByYear(IEnumerable<ClosedTradeModel> closedTrades) {
        return closedTrades
            .GroupBy(trade => trade.ClosingTime.Year)
            .ToDictionary(yearTrades => yearTrades.Key, yearTrades => yearTrades.Sum(trade => trade.NetProfit));
    }

    // Years outside the window are folded into the nearest edge year rather than dropped: the
    // window is the robot's own start and stop time, so anything outside it is a rounding artefact
    // of that clock, not a year the filter should ignore.
    private double[] ProfitOfEveryYearInWindow(Dictionary<int, double> profitByYear) {
        int firstYear = Math.Min(_firstYear, profitByYear.Keys.Min());
        int lastYear = Math.Max(_lastYear, profitByYear.Keys.Max());

        return Enumerable.Range(firstYear, lastYear - firstYear + 1)
            .Select(year => profitByYear.TryGetValue(year, out double profit) ? profit : 0d)
            .ToArray();
    }

    private static double RejectionScore(IEnumerable<double> yearlyProfits) {
        return yearlyProfits
            .Where(profit => profit <= 0)
            .Sum(profit => profit - FailedYearPenalty);
    }

    private static double BuiltInScore(FitnessStatsModel stats) {
        return stats.NetProfit * stats.WinningTrades / (1 + stats.MaxEquityDrawdownPercent / 100);
    }
}
