using System;

namespace cAlgo.Robots;

// One finished trade, reduced to the two fields the optimisation fitness reads. Keeps
// AnnualFitness free of cAlgo.API's HistoricalTrade so it can be unit tested.
public class ClosedTradeModel {
    public ClosedTradeModel(DateTime closingTime, double netProfit) {
        ClosingTime = closingTime;
        NetProfit = netProfit;
    }

    // Platform time, same clock and time zone as the robot (Tokyo). A trade opened in December
    // and closed in January therefore counts towards the year it closed in.
    public DateTime ClosingTime { get; }

    public double NetProfit { get; }
}
