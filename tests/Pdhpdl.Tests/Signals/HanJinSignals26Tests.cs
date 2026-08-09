using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Signals {
    public class HanJinSignals26Tests {
        // ── ① Pinbar ────────────────────────────────────────────────────────
        [Fact]
        public void pinbar_is_buy_when_long_lower_wick_and_small_upper_wick() {
            // range 10, body 9..10 (top), lower wick 9 (0.9), upper wick 0.
            CandleModel bar = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);
            Assert.Equal(SignalSideModel.Buy, HanJinSignals26.Pinbar(bar));
        }

        [Fact]
        public void pinbar_is_sell_when_long_upper_wick_and_small_lower_wick() {
            CandleModel bar = new(open: 1.0, high: 10.0, low: 0.0, close: 0.5);
            Assert.Equal(SignalSideModel.Sell, HanJinSignals26.Pinbar(bar));
        }

        [Fact]
        public void pinbar_is_none_when_candle_has_no_range() {
            CandleModel doji = new(open: 5.0, high: 5.0, low: 5.0, close: 5.0);
            Assert.Equal(SignalSideModel.None, HanJinSignals26.Pinbar(doji));
        }

        [Fact]
        public void pinbar_strict_flag_blocks_signal_when_opposite_wick_too_long() {
            // Long lower wick (0.7) but also a long upper wick (0.3 > 0.2) -> strict rejects.
            CandleModel bar = new(open: 7.0, high: 10.0, low: 0.0, close: 7.0);
            Assert.Equal(SignalSideModel.None, HanJinSignals26.Pinbar(bar));
            HanJinSignalOptionsModel lenient = new() { PinbarStrict = false };
            Assert.Equal(SignalSideModel.Buy, HanJinSignals26.Pinbar(bar, lenient));
        }

        // ── ② Engulf ────────────────────────────────────────────────────────
        [Fact]
        public void engulf_follows_current_body_direction_when_it_brackets_previous() {
            CandleModel previous = new(open: 5.0, high: 6.0, low: 4.0, close: 4.5);
            CandleModel current = new(open: 3.0, high: 7.0, low: 2.0, close: 6.5);
            Assert.Equal(SignalSideModel.Buy, HanJinSignals26.Engulf(current, previous));
        }

        [Fact]
        public void engulf_is_none_when_current_does_not_cover_previous() {
            CandleModel previous = new(open: 5.0, high: 8.0, low: 4.0, close: 7.0);
            CandleModel current = new(open: 5.5, high: 6.0, low: 5.0, close: 5.8);
            Assert.Equal(SignalSideModel.None, HanJinSignals26.Engulf(current, previous));
        }

        // ── ③ Fractal ───────────────────────────────────────────────────────
        // Fractal needs two things: the middle bar [1] dominating both neighbours on the high
        // AND the low line, plus the current bar [0] confirming — closing past the middle bar's
        // body in the signal's direction, with the right body direction and no long wick against
        // it. A structurally perfect fractal with a non-confirming current bar is None.
        [Fact]
        public void fractal_top_is_sell_when_middle_bar_dominates_and_current_bar_confirms() {
            CandleModel current = new(open: 6.0, high: 6.0, low: 4.0, close: 4.5);   // [0] right, bearish
            CandleModel middle = new(open: 8.0, high: 9.0, low: 7.0, close: 8.0);    // [1]
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);   // [2] left
            (SignalSideModel top, SignalSideModel bottom) = HanJinSignals26.Fractal(current, middle, earlier);
            Assert.Equal(SignalSideModel.Sell, top);
            Assert.Equal(SignalSideModel.None, bottom);
        }

        [Fact]
        public void fractal_bottom_is_buy_when_middle_bar_dominates_and_current_bar_confirms() {
            CandleModel current = new(open: 4.0, high: 6.0, low: 4.0, close: 5.5);   // [0] right, bullish
            CandleModel middle = new(open: 2.0, high: 3.0, low: 1.0, close: 2.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            (SignalSideModel top, SignalSideModel bottom) = HanJinSignals26.Fractal(current, middle, earlier);
            Assert.Equal(SignalSideModel.Buy, bottom);
            Assert.Equal(SignalSideModel.None, top);
        }

        [Fact]
        public void fractal_is_none_when_the_current_bar_does_not_confirm() {
            CandleModel doji = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);      // no body direction
            CandleModel middle = new(open: 8.0, high: 9.0, low: 7.0, close: 8.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            (SignalSideModel top, SignalSideModel bottom) = HanJinSignals26.Fractal(doji, middle, earlier);
            Assert.Equal(SignalSideModel.None, top);
            Assert.Equal(SignalSideModel.None, bottom);
        }

        // ── ④ Harami ─────────────────────────────────────────────────────────
        [Fact]
        public void harami_is_buy_when_close_breaks_above_inside_bar() {
            CandleModel current = new(open: 5.0, high: 8.0, low: 4.0, close: 7.5);
            CandleModel insideBar = new(open: 3.0, high: 7.0, low: 2.0, close: 6.0);
            CandleModel parent = new(open: 1.0, high: 10.0, low: 0.0, close: 9.0);

            (SignalSideModel single, SignalSideModel doubleHarami) = HanJinSignals26.Harami(current, insideBar, parent);

            Assert.Equal(SignalSideModel.Buy, single);
            Assert.Equal(SignalSideModel.Buy, doubleHarami);
        }

        [Fact]
        public void harami_is_sell_when_close_breaks_below_inside_bar() {
            CandleModel current = new(open: 5.0, high: 6.0, low: 1.0, close: 1.5);
            CandleModel insideBar = new(open: 3.0, high: 7.0, low: 2.0, close: 6.0);
            CandleModel parent = new(open: 1.0, high: 10.0, low: 0.0, close: 9.0);

            (SignalSideModel single, SignalSideModel doubleHarami) = HanJinSignals26.Harami(current, insideBar, parent);

            Assert.Equal(SignalSideModel.Sell, single);
            Assert.Equal(SignalSideModel.Sell, doubleHarami);
        }

        [Fact]
        public void harami_is_none_when_close_remains_inside_previous_range() {
            CandleModel current = new(open: 5.0, high: 6.0, low: 4.0, close: 5.5);
            CandleModel insideBar = new(open: 3.0, high: 7.0, low: 2.0, close: 6.0);
            CandleModel parent = new(open: 1.0, high: 10.0, low: 0.0, close: 9.0);

            (SignalSideModel single, SignalSideModel doubleHarami) = HanJinSignals26.Harami(current, insideBar, parent);

            Assert.Equal(SignalSideModel.None, single);
            Assert.Equal(SignalSideModel.None, doubleHarami);
        }

        // ── ⑤ Big Body ───────────────────────────────────────────────────────
        [Fact]
        public void bigbody_is_sell_when_bearish_body_fills_most_of_the_range() {
            CandleModel bar = new(open: 9.5, high: 10.0, low: 0.0, close: 0.5);
            Assert.Equal(SignalSideModel.Sell, HanJinSignals26.BigBody(bar));
        }

        [Fact]
        public void bigbody_is_none_when_body_is_small_relative_to_range() {
            CandleModel bar = new(open: 5.0, high: 10.0, low: 0.0, close: 5.5);
            Assert.Equal(SignalSideModel.None, HanJinSignals26.BigBody(bar));
        }

        // ── Aggregate ─────────────────────────────────────────────────────────
        [Fact]
        public void scan_reports_each_pattern_side_for_the_window() {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);   // buy pinbar
            CandleModel previous = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);

            HanJinSignalScanModel scan = HanJinSignals26.Scan(current, previous, earlier);

            Assert.Equal(SignalSideModel.Buy, scan.Pinbar);
            Assert.Equal(HanJinSignals26.Engulf(current, previous), scan.Engulf);
            Assert.Equal(HanJinSignals26.BigBody(current), scan.BigBody);
        }
    }
}
