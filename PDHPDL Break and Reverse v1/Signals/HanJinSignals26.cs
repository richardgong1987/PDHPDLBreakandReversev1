namespace cAlgo.Robots;

// C# port of the Pine library HanJinSignals26 (© richardgong1988). Pure classifier: given a
// candle (or a 3-bar window ordered current -> previous -> earlier, i.e. Pine offsets
// [0],[1],[2]), it returns which pattern fired and on which side. No cAlgo dependency, so it
// is unit tested. Design: docs/design/hanjin-signals-26.md.
public static class HanJinSignals26 {
    // Shared read-only defaults so the parameterless overloads reproduce the Pine defaults
    // without allocating. Never mutated.
    private static readonly HanJinSignalOptionsModel DefaultOptions = new();

    // ── Aggregate ─────────────────────────────────────────────────────────────
    // Runs every pattern over the window. earlier/previous are only read by the multi-bar
    // patterns; single-bar patterns look at current alone.
    public static HanJinSignalScanModel Scan(CandleModel current, CandleModel previous, CandleModel earlier) =>
        Scan(current, previous, earlier, DefaultOptions);

    public static HanJinSignalScanModel Scan(CandleModel current, CandleModel previous, CandleModel earlier,
        HanJinSignalOptionsModel options) {
        (SignalSideModel top, SignalSideModel bottom) = Fractal(current, previous, earlier);
        (SignalSideModel single, SignalSideModel doubleHarami) = Harami(current, previous, earlier);

        return new HanJinSignalScanModel {
            Pinbar = Pinbar(current, options),
            Engulf = Engulf(current, previous),
            FractalTop = top,
            FractalBottom = bottom,
            HaramiSingle = single,
            HaramiDouble = doubleHarami,
            BigBody = BigBody(current, options)
        };
    }

    // ── ① Pinbar ──────────────────────────────────────────────────────────────
    public static SignalSideModel Pinbar(CandleModel bar) => Pinbar(bar, DefaultOptions);

    public static SignalSideModel Pinbar(CandleModel bar, HanJinSignalOptionsModel options) {
        if (!bar.HasRange)
            return SignalSideModel.None;

        double upperWick = (bar.High - bar.BodyTop) / bar.Range;
        double lowerWick = (bar.BodyBottom - bar.Low) / bar.Range;

        // 下引线长。上引线短
        bool isBull = lowerWick >= options.PinbarLongFraction && (!options.PinbarStrict || upperWick <= options.PinbarShortFraction);

        // 上引线长，下引线短。
        bool isBear = upperWick >= options.PinbarLongFraction && (!options.PinbarStrict || lowerWick <= options.PinbarShortFraction);

        return isBull ? SignalSideModel.Buy : isBear ? SignalSideModel.Sell : SignalSideModel.None;
    }

    // ── ② Engulfing ───────────────────────────────────────────────────────────
    public static SignalSideModel Engulf(CandleModel current, CandleModel previous) {
        bool isEngulfing = current.High >= previous.High && current.Low <= previous.Low && current.BodyTop >= previous.BodyTop &&
                           current.BodyBottom <= previous.BodyBottom;

        return isEngulfing ? FollowBody(current.BodyDirection) : SignalSideModel.None;
    }

    // ── ③ Fractal — returns (Top, Bottom) ─────────────────────────────────────
    // Strict structural fractal: the middle bar (previous, [1]) dominates BOTH neighbours on
    // the high line AND the low line.
    public static (SignalSideModel Top, SignalSideModel Bottom) Fractal(CandleModel current, CandleModel previous, CandleModel earlier) {
        bool isTop = previous.High > earlier.High && previous.High > current.High && previous.Low > earlier.Low &&
                     previous.Low > current.Low;
        bool isBottom = previous.Low < earlier.Low && previous.Low < current.Low && previous.High < earlier.High &&
                        previous.High < current.High;

        return (isTop ? SignalSideModel.Sell : SignalSideModel.None, isBottom ? SignalSideModel.Buy : SignalSideModel.None);
    }

    // ── ④ Harami + double Harami — returns (Single, Double) ────────────────────
    public static (SignalSideModel Single, SignalSideModel Double) Harami(CandleModel current, CandleModel previous, CandleModel earlier) {
        bool parentContainsCurrent = Contains(outer: previous, inner: current);
        bool grandparentContainsParent = Contains(outer: earlier, inner: previous);

        SignalSideModel single = parentContainsCurrent ? ReverseBody(previous.BodyDirection) : SignalSideModel.None;
        SignalSideModel doubleHarami = parentContainsCurrent && grandparentContainsParent
            ? ReverseBody(earlier.BodyDirection)
            : SignalSideModel.None;

        return (single, doubleHarami);
    }

    // ── ⑤ Big Body ────────────────────────────────────────────────────────────
    public static SignalSideModel BigBody(CandleModel bar) => BigBody(bar, DefaultOptions);

    public static SignalSideModel BigBody(CandleModel bar, HanJinSignalOptionsModel options) {
        if (!bar.HasRange)
            return SignalSideModel.None;

        double bodyFraction = System.Math.Abs(bar.Close - bar.Open) / bar.Range;
        return bodyFraction >= options.BigBodyMinFraction ? FollowBody(bar.BodyDirection) : SignalSideModel.None;
    }

    // ── Internal geometry helpers (mirror the Pine private functions) ──────────
    // outer fully brackets inner on both the high and the low line.
    private static bool Contains(CandleModel outer, CandleModel inner) =>
        outer.High >= inner.High && outer.Low <= inner.Low;

    // Continuation: the signal follows the body direction (up -> Buy).
    private static SignalSideModel FollowBody(int bodyDirection) =>
        bodyDirection > 0 ? SignalSideModel.Buy : bodyDirection < 0 ? SignalSideModel.Sell : SignalSideModel.None;

    // Reversal: the signal opposes the body direction (up -> Sell). Pine's reverseOf.
    private static SignalSideModel ReverseBody(int bodyDirection) =>
        bodyDirection > 0 ? SignalSideModel.Sell : bodyDirection < 0 ? SignalSideModel.Buy : SignalSideModel.None;
}
