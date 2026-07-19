namespace cAlgo.Robots;

// 「策略模式」：选择生产策略，或只回测单个信号的单个方向。
// 由 StrategyModePolicy 翻译成 MainBiz 需要的三个开关：方向 / 信号家族 / 突破分支。
public enum StrategyModel {
    // ── 生产模式：Pinbar/Engulf/分型/Harami 全部信号一起跑，多空由 RMA 过滤 ──
    All,           // 全部条件。不作隔离
    Reversal,      // 只开「假突破/反转」
    Continuation,  // 只开「真突破/延续」

    PinbarLong, // PinBar作多
    PinbarShort,// PinBar作空
    EngulfLong,// PinBar作多
    EngulfShort,// PinBar作空
    HaramiLong,// PinBar作多
    HaramiShort,// PinBar作空
    FractalTopShort,    // 顶分型看跌，只作空
    FractalBottomLong,  // 底分型看多，只作多
}
