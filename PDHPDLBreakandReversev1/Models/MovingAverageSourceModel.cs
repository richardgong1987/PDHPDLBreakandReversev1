namespace cAlgo.Robots;

// Which price series the RMA overlay is computed on.
public enum MovingAverageSourceModel {
    // Wilder RMA on the 120-minute (or configured) higher timeframe, rendered as
    // a stepped line held until each higher-timeframe bar confirms. This is the
    // faithful port of the Pine request.security(tickerid, "120", ...) behaviour.
    HigherTimeFrame,

    // Wilder RMA on the chart's own timeframe (Pine's ma_1_normal). Updates every
    // chart bar, so it tracks price without the higher-timeframe stepping lag.
    ChartTimeFrame
}
