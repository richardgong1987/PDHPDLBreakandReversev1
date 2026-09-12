namespace cAlgo.Robots;

// 开口扩大闸门：趋势均线的开口已经拉开还不够，得在「继续拉开」才放行。
// GapX 是多头视角（快线 - 慢线）的带符号值，空头的开口正好是它的相反数，
// 所以作空时取负再比 —— 两个方向共用同一个阈值。纯逻辑，单元测试覆盖。
public static class GapXGate {
    public static bool IsExpanding(bool isEnabled, double gapX, double threshold, SignalSideModel side) {
        // 闸门关闭时连 GapX 都不看：即使算不出来也照常放行。
        if (!isEnabled)
            return true;

        // 闸门开着却读不到 GapX（均线未确认、ATR 暖机、历史不够）就挡掉：
        // 这种时候放行，等于把这道风控静默关掉。
        if (double.IsNaN(gapX))
            return false;

        double expansion = side == SignalSideModel.Buy ? gapX : -gapX;
        return expansion >= threshold;
    }
}
