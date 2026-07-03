using System;

namespace cAlgo.Robots;

// Pure PDH/PDL false-breakout rules. No cAlgo dependency, so these are unit tested.
public static class PdhpdlSignalRules {
    // Short: the bar pierced a level (PDH or PDL) but closed back below it.
    public static bool IsShortSignal(double high, double open, double close, double pdh, double pdl) {
        double body = Math.Max(open, close);
        bool rejectedFromPdh = high > pdh && pdh > body;
        bool rejectedFromPdl = high > pdl && pdl > body;
        return rejectedFromPdh || rejectedFromPdl;
    }

    // Long: the bar pierced a level (PDH or PDL) but closed back above it.
    public static bool IsLongSignal(double low, double open, double close, double pdh, double pdl) {
        double body = Math.Min(open, close);
        bool rejectedFromPdh = pdh > low && body > pdh;
        bool rejectedFromPdl = pdl > low && body > pdl;
        return rejectedFromPdh || rejectedFromPdl;
    }
}
