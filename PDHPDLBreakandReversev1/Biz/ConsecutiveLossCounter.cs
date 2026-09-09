namespace cAlgo.Robots;

// 连亏计数器：只数「连续亏了几笔」，然后决定结构点闸门（PivotEntryGate）这一关要不要走。
//
// 亏一笔加一，赚一笔清零（不赚不亏按不亏算，也清零）。多空合起来数一个数，
// 不分方向：连亏指的是这个策略连着吃了几笔亏，跟这几笔是作多还是作空无关。
//
// 连亏笔数够了 Nlock 笔，入场才要求结构点站在自己这一边（见 MainBiz）；没到就照常放行。
// 所以它是给闸门用的开关，不是锁仓 —— 到了 Nlock 也不停手，只是把门槛抬上去。
//
// Nlock <= 0 表示不启用：闸门永远不查。
public class ConsecutiveLossCounter {
    private readonly int _maxConsecutiveLosses;

    public ConsecutiveLossCounter(int maxConsecutiveLosses) {
        _maxConsecutiveLosses = maxConsecutiveLosses;
    }

    public bool IsEnabled => _maxConsecutiveLosses > 0;

    public int ConsecutiveLosses { get; private set; }

    // 连亏够了 Nlock 笔，从这一刻起每一笔入场都要先过结构点闸门，直到赢一笔把计数清零。
    public bool IsPivotGateRequired => IsEnabled && ConsecutiveLosses >= _maxConsecutiveLosses;

    // 换线清零：PDH/PDL 每天换一对，昨天那几笔亏损属于昨天那对关键位，不带进新的一天
    //（见 PdhpdlSignalDetector）。
    public void Reset() {
        ConsecutiveLosses = 0;
    }

    public void RecordClosedTrade(double netProfit) {
        if (netProfit < 0) {
            ConsecutiveLosses++;
            return;
        }

        ConsecutiveLosses = 0;
    }
}
