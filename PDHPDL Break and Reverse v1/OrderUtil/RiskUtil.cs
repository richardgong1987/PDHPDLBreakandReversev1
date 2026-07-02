using System;

namespace cAlgo.Robots {
    public static class RiskUtil {
        /**
         * 比如。
         * 1.余额=10000，风险百分比=1%，所以愿意亏：100
         * 2.余额=10000，风险百分比=2%，所以愿意亏：200
         * 3.余额=12345，风险百分比=1%，所以愿意亏：123.45
         */
        public static double CalcRiskMoney(double equity, double riskPct) {
            if (equity <= 0.0 || riskPct <= 0.0)
                return 0.0;
            return equity * riskPct / 100.0;
        }
    }
}
