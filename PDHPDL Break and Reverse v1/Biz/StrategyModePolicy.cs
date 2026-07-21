using System.Collections.Generic;

namespace cAlgo.Robots;

// 把「策略模式」枚举翻译成 MainBiz 需要的四个正交开关：方向、信号家族、突破分支、关键位。
// 生产模式(All/Reversal/Continuation)跑全部信号、多空皆可(由 RMA 过滤)；
// 隔离测试模式只跑单个信号家族；方向、关键位可再收窄到单边(Long/Short)或单个关键位(PDH/PDL)。
// 这里是唯一知道模式分类的地方，MainBiz 与各信号 predicate 都不直接判断枚举值。
public static class StrategyModePolicy {
    // 方向过滤：Both = 多空都跑；Long/Short = 只跑单边。
    private enum DirectionFilter {
        Both,
        Long,
        Short
    }

    // 关键位过滤：Both = PDH/PDL 两个分支都跑；Pdh/Pdl = 只跑触碰该关键位的分支。
    private enum LevelFilter {
        Both,
        Pdh,
        Pdl
    }

    private readonly struct IsolationSpec {
        public IsolationSpec(SignalFamilyModel family, DirectionFilter direction, LevelFilter keyLevel) {
            Family = family;
            Direction = direction;
            KeyLevel = keyLevel;
        }

        public SignalFamilyModel Family { get; }
        public DirectionFilter Direction { get; }
        public LevelFilter KeyLevel { get; }
    }

    private static IsolationSpec Iso(SignalFamilyModel family, DirectionFilter direction, LevelFilter keyLevel) {
        return new IsolationSpec(family, direction, keyLevel);
    }

    // 每个隔离模式绑定的 (信号家族, 方向, 关键位)。不在表中的即生产模式(放行一切)。
    // 与老板给的清单一一对应：先是「只隔离家族、多空都跑」，再是「家族+方向」，再是「家族+方向+关键位」。
    private static readonly Dictionary<StrategyModel, IsolationSpec> IsolationSpecs = new() {
        { StrategyModel.PinbarLong, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Long, LevelFilter.Both) },
        { StrategyModel.PinbarShort, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Short, LevelFilter.Both) },
        { StrategyModel.EngulfLong, Iso(SignalFamilyModel.Engulf, DirectionFilter.Long, LevelFilter.Both) },
        { StrategyModel.EngulfShort, Iso(SignalFamilyModel.Engulf, DirectionFilter.Short, LevelFilter.Both) },
        { StrategyModel.HaramiLong, Iso(SignalFamilyModel.Harami, DirectionFilter.Long, LevelFilter.Both) },
        { StrategyModel.HaramiShort, Iso(SignalFamilyModel.Harami, DirectionFilter.Short, LevelFilter.Both) },
        { StrategyModel.FractalTopShort, Iso(SignalFamilyModel.FractalTop, DirectionFilter.Short, LevelFilter.Both) },
        { StrategyModel.FractalBottomLong, Iso(SignalFamilyModel.FractalBottom, DirectionFilter.Long, LevelFilter.Both) },
        { StrategyModel.PdhPinbarLong, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhPinbarShort, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhEngulfLong, Iso(SignalFamilyModel.Engulf, DirectionFilter.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhEngulfShort, Iso(SignalFamilyModel.Engulf, DirectionFilter.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhHaramiLong, Iso(SignalFamilyModel.Harami, DirectionFilter.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhHaramiShort, Iso(SignalFamilyModel.Harami, DirectionFilter.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhFractalTopShort, Iso(SignalFamilyModel.FractalTop, DirectionFilter.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhFractalBottomLong, Iso(SignalFamilyModel.FractalBottom, DirectionFilter.Long, LevelFilter.Pdh) },
        { StrategyModel.PdlPinbarLong, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlPinbarShort, Iso(SignalFamilyModel.Pinbar, DirectionFilter.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlEngulfLong, Iso(SignalFamilyModel.Engulf, DirectionFilter.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlEngulfShort, Iso(SignalFamilyModel.Engulf, DirectionFilter.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlHaramiLong, Iso(SignalFamilyModel.Harami, DirectionFilter.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlHaramiShort, Iso(SignalFamilyModel.Harami, DirectionFilter.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlFractalTopShort, Iso(SignalFamilyModel.FractalTop, DirectionFilter.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlFractalBottomLong, Iso(SignalFamilyModel.FractalBottom, DirectionFilter.Long, LevelFilter.Pdl) },
    };

    // 是否允许作多。生产模式恒允许；隔离模式当其绑定方向为 Long 或 Both。
    public static bool AllowsLong(StrategyModel mode) {
        return !IsolationSpecs.TryGetValue(mode, out IsolationSpec spec) || spec.Direction != DirectionFilter.Short;
    }

    // 是否允许作空。生产模式恒允许；隔离模式当其绑定方向为 Short 或 Both。
    public static bool AllowsShort(StrategyModel mode) {
        return !IsolationSpecs.TryGetValue(mode, out IsolationSpec spec) || spec.Direction != DirectionFilter.Long;
    }

    // 是否放行某个信号家族。生产模式全部放行；隔离模式仅放行其绑定家族。
    public static bool AllowsFamily(StrategyModel mode, SignalFamilyModel family) {
        return !IsolationSpecs.TryGetValue(mode, out IsolationSpec spec) || spec.Family == family;
    }

    // 「假突破/反转」分支开关（原 Utils.CanA）。
    public static bool AllowsReversal(StrategyModel mode) {
        if (!IsolationSpecs.TryGetValue(mode, out IsolationSpec spec))
            return mode != StrategyModel.Continuation; // 生产：只做真突破时关掉反转分支

        return spec.KeyLevel == LevelFilter.Both || TargetsReversalBranch(spec);
    }

    // 「真突破/延续」分支开关（原 Utils.CanB）。
    public static bool AllowsContinuation(StrategyModel mode) {
        if (!IsolationSpecs.TryGetValue(mode, out IsolationSpec spec))
            return mode != StrategyModel.Reversal; // 生产：只做假突破时关掉延续分支

        return spec.KeyLevel == LevelFilter.Both || !TargetsReversalBranch(spec);
    }

    // 关键位隔离模式命中的唯一分支（仅在 KeyLevel 为 Pdh/Pdl 时调用，此时方向必为单边）：
    //   Short+PDH、Long+PDL → 假突破/反转 分支
    //   Short+PDL、Long+PDH → 真突破/延续 分支
    private static bool TargetsReversalBranch(IsolationSpec spec) {
        bool touchesPdh = spec.KeyLevel == LevelFilter.Pdh;
        bool isShort = spec.Direction == DirectionFilter.Short;
        return touchesPdh == isShort;
    }
}
