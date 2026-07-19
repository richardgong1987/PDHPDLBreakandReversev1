using System;

namespace cAlgo.Robots;

// 把「策略模式」枚举翻译成 MainBiz 需要的三个正交开关：方向、信号家族、突破分支。
// 生产模式(All/Reversal/Continuation)跑全部信号、多空皆可(由 RMA 过滤)；
// 隔离测试模式只跑单个信号的单个方向，且「假突破」「真突破」两个分支都开。
// 这里是唯一知道模式分类的地方，MainBiz 不再直接判断枚举值。
public static class StrategyModePolicy {
    // 是否允许作多。生产模式恒允许；隔离模式仅当其绑定方向为 Long。
    public static bool AllowsLong(StrategyModel mode) {
        return IsProductionMode(mode) || DirectionOf(mode) == PdhpdlTradeDirectionModel.Long;
    }

    // 是否允许作空。生产模式恒允许；隔离模式仅当其绑定方向为 Short。
    public static bool AllowsShort(StrategyModel mode) {
        return IsProductionMode(mode) || DirectionOf(mode) == PdhpdlTradeDirectionModel.Short;
    }

    // 是否放行某个信号家族。生产模式全部放行；隔离模式仅放行其绑定家族。
    public static bool AllowsFamily(StrategyModel mode, SignalFamilyModel family) {
        return IsProductionMode(mode) || FamilyOf(mode) == family;
    }

    // 「假突破/反转」分支开关（原 Utils.CanA = AB || A）。除「只做真突破」外都开。
    public static bool AllowsReversal(StrategyModel mode) {
        return mode != StrategyModel.Continuation;
    }

    // 「真突破/延续」分支开关（原 Utils.CanB = AB || B）。除「只做假突破」外都开。
    public static bool AllowsContinuation(StrategyModel mode) {
        return mode != StrategyModel.Reversal;
    }

    private static bool IsProductionMode(StrategyModel mode) {
        return mode == StrategyModel.All || mode == StrategyModel.Reversal || mode == StrategyModel.Continuation;
    }

    private static SignalFamilyModel FamilyOf(StrategyModel mode) {
        switch (mode) {
            case StrategyModel.PinbarLong:
            case StrategyModel.PinbarShort:
                return SignalFamilyModel.Pinbar;
            case StrategyModel.EngulfLong:
            case StrategyModel.EngulfShort:
                return SignalFamilyModel.Engulf;
            case StrategyModel.HaramiLong:
            case StrategyModel.HaramiShort:
                return SignalFamilyModel.Harami;
            case StrategyModel.FractalTopShort:
                return SignalFamilyModel.FractalTop;
            case StrategyModel.FractalBottomLong:
                return SignalFamilyModel.FractalBottom;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "生产模式没有绑定的信号家族");
        }
    }

    private static PdhpdlTradeDirectionModel DirectionOf(StrategyModel mode) {
        switch (mode) {
            case StrategyModel.PinbarLong:
            case StrategyModel.EngulfLong:
            case StrategyModel.HaramiLong:
            case StrategyModel.FractalBottomLong:
                return PdhpdlTradeDirectionModel.Long;
            case StrategyModel.PinbarShort:
            case StrategyModel.EngulfShort:
            case StrategyModel.HaramiShort:
            case StrategyModel.FractalTopShort:
                return PdhpdlTradeDirectionModel.Short;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "生产模式没有绑定的方向");
        }
    }
}
