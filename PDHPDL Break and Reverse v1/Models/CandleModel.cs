using System;

namespace cAlgo.Robots;

// One candle's OHLC plus the geometry the HanJin patterns read (body edges, range,
// direction). Mirrors the private helpers of the Pine library so the pattern code stays a
// direct, readable translation. Pure value type — no cAlgo dependency.
public readonly struct CandleModel {
    public CandleModel(double open, double high, double low, double close) {
        Open = open;
        High = high;
        Low = low;
        Close = close;
    }

    public double Open { get; }

    public double High { get; }

    public double Low { get; }

    public double Close { get; }

    public double Range => High - Low;

    public bool HasRange => Range > 0;

    // Top and bottom of the real body (candle without wicks).
    public double BodyTop => Math.Max(Open, Close);

    public double BodyBottom => Math.Min(Open, Close);

    // +1 bullish, -1 bearish, 0 doji. Pine's bodyDirAt.
    public int BodyDirection => Close > Open ? 1 : Close < Open ? -1 : 0;

    public bool IsBullish => BodyDirection == 1;

    public bool IsBearish => BodyDirection == -1;

    // 长引线的门槛：引线超过整根 K 线振幅的 40%。
    private const double LongWickMinFraction = 0.4;

    // 是否上引线过大，超过40%
    public bool HasLongUpperWick => HasRange && (High - BodyTop) / Range > LongWickMinFraction;

    // 是否下引线过大，超过40%
    public bool HasLongLowerWick => HasRange && (BodyBottom - Low) / Range > LongWickMinFraction;
}
