using System.Collections.Generic;

namespace cAlgo.Robots;

// 把「策略模式」枚举翻译成 MainBiz 需要的四个正交开关：方向、信号家族、突破分支、关键位。
// 生产模式(All/Reversal/Continuation)跑全部信号、多空皆可(由 RMA 过滤)；
// 隔离测试模式只跑单个信号的单个方向；关键位隔离模式再进一步只跑触碰指定关键位(PDH/PDL)的那个分支。
// 这里是唯一知道模式分类的地方，MainBiz 与各信号 predicate 都不直接判断枚举值。
public static class StrategyModePolicy {
    // 关键位过滤：Both = PDH/PDL 两个分支都跑；Pdh/Pdl = 只跑触碰该关键位的分支。
    private enum LevelFilter {
        Both,
        Pdh,
        Pdl
    }

    private readonly struct IsolationSpec {
        public IsolationSpec(SignalFamilyModel family, PdhpdlTradeDirectionModel direction, LevelFilter keyLevel) {
            Family = family;
            Direction = direction;
            KeyLevel = keyLevel;
        }

        public SignalFamilyModel Family { get; }
        public PdhpdlTradeDirectionModel Direction { get; }
        public LevelFilter KeyLevel { get; }
    }

    // 每个隔离模式绑定的 (信号家族, 方向, 关键位)。不在表中的即生产模式(放行一切)。
    // 与老板给的清单一一对应：先是「PDH/PDL 都跑」的家族隔离，再是 PDH-only、PDL-only。
    private static readonly Dictionary<StrategyModel, IsolationSpec> IsolationSpecs = new() {
        { StrategyModel.PinbarLong, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Long, LevelFilter.Both) },
        { StrategyModel.PinbarShort, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Short, LevelFilter.Both) },
        { StrategyModel.EngulfLong, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Long, LevelFilter.Both) },
        { StrategyModel.EngulfShort, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Short, LevelFilter.Both) },
        { StrategyModel.HaramiLong, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Long, LevelFilter.Both) },
        { StrategyModel.HaramiShort, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Short, LevelFilter.Both) }, {
            StrategyModel.FractalTopShort,
            new IsolationSpec(SignalFamilyModel.FractalTop, PdhpdlTradeDirectionModel.Short, LevelFilter.Both)
        }, {
            StrategyModel.FractalBottomLong,
            new IsolationSpec(SignalFamilyModel.FractalBottom, PdhpdlTradeDirectionModel.Long, LevelFilter.Both)
        },
        { StrategyModel.PdhPinbarLong, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhPinbarShort, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhEngulfLong, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhEngulfShort, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdh) },
        { StrategyModel.PdhHaramiLong, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdh) },
        { StrategyModel.PdhHaramiShort, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdh) }, {
            StrategyModel.PdhFractalTopShort,
            new IsolationSpec(SignalFamilyModel.FractalTop, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdh)
        }, {
            StrategyModel.PdhFractalBottomLong,
            new IsolationSpec(SignalFamilyModel.FractalBottom, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdh)
        },
        { StrategyModel.PdlPinbarLong, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlPinbarShort, new IsolationSpec(SignalFamilyModel.Pinbar, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlEngulfLong, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlEngulfShort, new IsolationSpec(SignalFamilyModel.Engulf, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdl) },
        { StrategyModel.PdlHaramiLong, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdl) },
        { StrategyModel.PdlHaramiShort, new IsolationSpec(SignalFamilyModel.Harami, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdl) }, {
            StrategyModel.PdlFractalTopShort,
            new IsolationSpec(SignalFamilyModel.FractalTop, PdhpdlTradeDirectionModel.Short, LevelFilter.Pdl)
        }, {
            StrategyModel.PdlFractalBottomLong,
            new IsolationSpec(SignalFamilyModel.FractalBottom, PdhpdlTradeDirectionModel.Long, LevelFilter.Pdl)
        },
    };

    // 是否允许作多。生产模式恒允许；隔离模式仅当其绑定方向为 Long。
    public static bool AllowsLong(StrategyModel mode) {
        return !IsolationSpecs.TryGetValue(mode, out IsolationSpec spec) || spec.Direction == PdhpdlTradeDirectionModel.Long;
    }

    // 是否允许作空。生产模式恒允许；隔离模式仅当其绑定方向为 Short。
    public static bool AllowsShort(StrategyModel mode) {
        return !IsolationSpecs.TryGetValue(mode, out IsolationSpec spec) || spec.Direction == PdhpdlTradeDirectionModel.Short;
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

    // 关键位隔离模式命中的唯一分支：
    //   Short+PDH、Long+PDL → 假突破/反转 分支
    //   Short+PDL、Long+PDH → 真突破/延续 分支
    private static bool TargetsReversalBranch(IsolationSpec spec) {
        bool touchesPdh = spec.KeyLevel == LevelFilter.Pdh;
        bool isShort = spec.Direction == PdhpdlTradeDirectionModel.Short;
        return touchesPdh == isShort;
    }
}
