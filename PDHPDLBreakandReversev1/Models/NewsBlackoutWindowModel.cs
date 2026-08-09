using System;

namespace cAlgo.Robots;

// A [Start, End) window during which no new orders open and open exposure is closed.
public class NewsBlackoutWindowModel {
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
