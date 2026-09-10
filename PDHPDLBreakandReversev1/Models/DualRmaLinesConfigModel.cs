using cAlgo.API;

namespace cAlgo.Robots;

// Tunables for the "MA 1 + MA 2" overlay (see LineDrawer/DualRmaLines).
// Pure data: no cAlgo.API references so it stays trivially inspectable.
// Defaults mirror the source Pine indicator: two Welles-Wilder (RMA) moving
// averages of period 13 and 55 computed on the 120-minute timeframe.
public class DualRmaLinesConfigModel {
    public MovingAverageSourceModel Source { get; set; } = MovingAverageSourceModel.HigherTimeFrame;

    public int FastPeriod { get; set; } = 13;

    public int SlowPeriod { get; set; } = 55;

    public TimeFrameSelectModel HigherTimeFrameMinutes { get; set; }

    public int Thickness { get; set; } = 3;
}
