using System;

namespace cAlgo.Robots {
    /// <summary>
    /// Pure position-sizing rules: given account risk tolerance and a stop distance,
    /// decide how much volume to trade. Independent of the cAlgo framework so it can be
    /// unit-tested without a running cBot or market connection.
    /// </summary>
    public static class RiskUtil {
        /// <summary>
        /// Money the account is willing to lose on a single trade, derived from equity and
        /// a risk percentage (e.g. 1.0 means risk 1% of equity).
        /// </summary>
        public static double CalcRiskMoney(double equity, double riskPct) {
            if (equity <= 0.0 || riskPct <= 0.0)
                return 0.0;
            return equity * riskPct / 100.0;
        }
    }
}
