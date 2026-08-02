namespace cAlgo.Robots;

// 连亏锁仓：连续亏损达到 Nlock 笔就锁住不再开新单，直到后面出现一根「走出来」的 K 线。
//
// 走出来 = 该 K 线振幅（High - Low）> 上锁那笔亏损单「开仓时」ATR 的 maxBarRangeAtr 倍。
// 只看单根大小，不累计、不看方向。
//
// 参照用的是上锁时冻结的 ATR，不是当前 ATR：行情冷下来时当前 ATR 会一起降、门槛跟着降，
// 等于自动解锁，锁仓就白锁了。
//
// 解锁是一次性的：任意一根 K 线达标就解开，之后再小的 K 线也不会重新锁上。
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

    // 上锁时冻结的 ATR，以及由它换算出的解锁门槛，供日志核对。
    public double LockedAtr { get; private set; }

    public double RequiredBarRange => LockedAtr * _maxBarRangeAtr;

    public void RecordClosedTrade(double netProfit, double atrAtEntry) {
        // 亏损口径与交易 CSV 的「盈利/亏损」一致：净盈亏为负才算亏，持平按盈利处理。
        if (netProfit >= 0.0) {
            ConsecutiveLosses = 0;
            Unlock();
            return;
        }

        ConsecutiveLosses++;

        if (!IsEnabled || ConsecutiveLosses < _maxConsecutiveLosses)
            return;

        // 每次达标都用最近这笔亏损单的开仓 ATR 重新上锁，参照始终跟着最近一次吃亏时的行情。
        IsLocked = true;
        LockedAtr = atrAtEntry;
    }

    // 每根收盘 K 线调用一次。
    public void RecordClosedBar(double high, double low) {
        if (!IsLocked)
            return;

        // 没记到冻结 ATR（例如重启后状态丢失）时直接放开：宁可多下一单，也不要无限期锁死。
        if (LockedAtr <= 0.0 || high - low > RequiredBarRange)
            Unlock();
    }

    private void Unlock() {
        IsLocked = false;
        LockedAtr = 0.0;
    }
}
