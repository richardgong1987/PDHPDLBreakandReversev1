namespace cAlgo.Robots;

// 「策略模式」：选择生产策略，或只回测单个信号（可再指定关键位 PDH/PDL）的单个方向。
// 由 StrategyModePolicy 翻译成 MainBiz 需要的四个开关：方向 / 信号家族 / 突破分支 / 关键位。
public enum StrategyModel {
    All, // 全部条件。不作隔离
    Reversal, // 只开「假突破/反转」
    Continuation, // 只开「真突破/延续」

    // ── 单信号隔离：多空都跑、PDH/PDL 都跑（只隔离信号家族）──
    Pinbar, // 只测 Pinbar，含作多+作空
    Engulf, // 只测 Engulf，含作多+作空
    Harami, // 只测 Harami，含作多+作空

    // ── 单信号隔离：PDH/PDL 两个分支都跑 ──
    PinbarLong, // PinBar作多
    PinbarShort, // PinBar作空
    EngulfLong, // Engulf作多
    EngulfShort, // Engulf作空
    HaramiLong, // Harami作多
    HaramiShort, // Harami作空
    FractalTopShort, // 顶分型看跌，只作空
    FractalBottomLong, // 底分型看多，只作多

    // ── 单信号 + 关键位隔离：只跑触碰 PDH 的那个分支 ──
    PdhPinbarLong, // PDH + PinBar作多
    PdhPinbarShort, // PDH + PinBar作空
    PdhEngulfLong, // PDH + Engulf作多
    PdhEngulfShort, // PDH + Engulf作空
    PdhHaramiLong, // PDH + Harami作多
    PdhHaramiShort, // PDH + Harami作空
    PdhFractalTopShort, // PDH + 顶分型看跌，只作空
    PdhFractalBottomLong, // PDH + 底分型看多，只作多

    // ── 单信号 + 关键位隔离：只跑触碰 PDL 的那个分支 ──
    PdlPinbarLong, // PDL + PinBar作多
    PdlPinbarShort, // PDL + PinBar作空
    PdlEngulfLong, // PDL + Engulf作多
    PdlEngulfShort, // PDL + Engulf作空
    PdlHaramiLong, // PDL + Harami作多
    PdlHaramiShort, // PDL + Harami作空
    PdlFractalTopShort, // PDL + 顶分型看跌，只作空
    PdlFractalBottomLong, // PDL + 底分型看多，只作多
}
