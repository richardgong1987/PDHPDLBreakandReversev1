namespace cAlgo.Robots;

// 连亏锁仓：连续亏损达到 Nlock 笔就锁住不再开新单，直到价格从那笔亏损单的入场价
// 跑出足够远——「走出来」了才恢复交易。
//
// 走出来 = 某根 K 线的最高价或最低价离锚点超过 maxBarRangeAtr × 冻结ATR。
// 量的是价格跑了多远，不是单根 K 线有多高：一波行情往往由几十根小 K 线堆出来，
// 按单根振幅判会几乎永远不解锁。方向不限，涨跌都算摆脱。
//
// 参照用的是上锁时冻结的 ATR，不是当前 ATR：行情冷下来时当前 ATR 会一起降、门槛跟着降，
// 等于自动解锁，锁仓就白锁了。
//
// 解锁是一次性的：任意一根 K 线达标就解开，之后价格回来也不会重新锁上。
// 重新上锁只发生在下一次连亏又达到 Nlock 笔时；赢一单则立刻清零并解锁。
public class ConsecutiveLossLock {
    private readonly int _maxConsecutiveLosses;
    private readonly double _maxBarRangeAtr;

    public ConsecutiveLossLock(int maxConsecutiveLosses, double maxBarRangeAtr) {
        _maxConsecutiveLosses = maxConsecutiveLosses;
        _maxBarRangeAtr = maxBarRangeAtr;
    }

    // Nlock <= 0 表示不启用锁仓。
    public bool IsEnabled => _maxConsecutiveLosses > 0;

    public int ConsecutiveLosses { get; private set; }

    public bool IsLocked { get; private set; }

    // 上锁时冻结的 ATR、量距离的锚点（那笔亏损单的入场价），以及换算出的解锁距离，供日志核对。
    public double LockedAtr { get; private set; }

    public double AnchorPrice { get; private set; }

    public double RequiredDistance => LockedAtr * _maxBarRangeAtr;

    public void RecordClosedTrade(double netProfit, double atrAtEntry, double entryPrice) {
        // 亏损口径与交易 CSV 的「盈利/亏损」一致：净盈亏为负才算亏，持平按盈利处理。
        if (netProfit >= 0.0) {
            ConsecutiveLosses = 0;
            Unlock();
            return;
        }

        ConsecutiveLosses++;

        if (!IsEnabled || ConsecutiveLosses < _maxConsecutiveLosses)
            return;

        // 每次达标都用最近这笔亏损单重新上锁，锚点与参照始终跟着最近一次吃亏。
        IsLocked = true;
        LockedAtr = atrAtEntry;
        AnchorPrice = entryPrice;
    }

    // 每根收盘 K 线调用一次。
    public void RecordClosedBar(double high, double low) {
        if (!IsLocked)
            return;

        // 没记到冻结 ATR（例如重启后状态丢失）时直接放开：宁可多下一单，也不要无限期锁死。
        if (LockedAtr <= 0.0 || HasMovedAway(high, low))
            Unlock();
    }

    private bool HasMovedAway(double high, double low) {
        return high - AnchorPrice > RequiredDistance || AnchorPrice - low > RequiredDistance;
    }

    private void Unlock() {
        IsLocked = false;
        LockedAtr = 0.0;
        AnchorPrice = 0.0;
    }
}
