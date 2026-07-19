namespace cAlgo.Robots;

// MainBiz 里的信号家族。隔离测试模式用它来只放行某一类信号。
public enum SignalFamilyModel {
    Pinbar, // 看涨/看跌 pinbar
    Engulf, // 看涨/看跌 吞没
    FractalTop, // 顶分型（看跌）
    FractalBottom, // 底分型（看涨）
    Harami // 孕线上破/下破
}
