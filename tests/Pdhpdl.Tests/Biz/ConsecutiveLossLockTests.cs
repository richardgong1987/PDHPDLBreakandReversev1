using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // Nlock = 2，倍数 = 3，冻结 ATR = 10 -> 解锁需要价格离锚点跑出 30 以上。
    public class ConsecutiveLossLockTests {
        private const int Nlock = 2;
        private const double BarRangeMultiple = 3.0;
        private const double EntryAtr = 10.0;
        private const double Anchor = 4000.0;
        private const double Loss = -10.0;
        private const double Win = 20.0;

        private static ConsecutiveLossLock CreateLock() => new(Nlock, BarRangeMultiple);

        private static ConsecutiveLossLock CreateLockedLock() {
            ConsecutiveLossLock lossLock = CreateLock();
            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);
            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);
            return lossLock;
        }

        [Fact]
        public void starts_unlocked() {
            Assert.False(CreateLock().IsLocked);
        }

        [Fact]
        public void stays_unlocked_before_the_streak_reaches_nlock() {
            ConsecutiveLossLock lossLock = CreateLock();

            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);

            Assert.Equal(1, lossLock.ConsecutiveLosses);
            Assert.False(lossLock.IsLocked);
        }

        [Fact]
        public void locks_when_the_streak_reaches_nlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            Assert.True(lossLock.IsLocked);
            Assert.Equal(EntryAtr, lossLock.LockedAtr);
            Assert.Equal(Anchor, lossLock.AnchorPrice);
            Assert.Equal(30.0, lossLock.RequiredDistance);
        }

        // 关键场景：一波行情由许多小 K 线堆出来，没有任何一根单独够大，
        // 但价格已经离锚点很远——必须解锁。旧的「单根振幅」规则在这里会一直锁着。
        [Fact]
        public void unlocks_when_many_small_bars_carry_price_far_away() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            for (int step = 1; step <= 5; step++) {
                double low = Anchor + (step - 1) * 7.0;
                lossLock.RecordClosedBar(high: low + 7.0, low: low); // 每根振幅只有 7，远小于 30
            }

            Assert.False(lossLock.IsLocked);
        }

        [Fact]
        public void a_bar_near_the_anchor_does_not_unlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 4020.0, low: 3990.0); // 离锚点最远 20 < 30

            Assert.True(lossLock.IsLocked);
        }

        // 门槛是严格大于：正好等于 3 × ATR 不算走出来。
        [Fact]
        public void a_bar_exactly_at_the_threshold_does_not_unlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 4030.0, low: 4020.0);

            Assert.True(lossLock.IsLocked);
        }

        [Fact]
        public void unlocks_when_price_runs_far_enough_up() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 4031.0, low: 4025.0);

            Assert.False(lossLock.IsLocked);
        }

        // 方向不限：跌得够远同样算摆脱。
        [Fact]
        public void unlocks_when_price_runs_far_enough_down() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 3975.0, low: 3969.0);

            Assert.False(lossLock.IsLocked);
        }

        // 解锁是一次性的：价格回到锚点附近也不会重新锁上。
        [Fact]
        public void stays_unlocked_after_price_comes_back() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 4031.0, low: 4025.0);
            lossLock.RecordClosedBar(high: 4002.0, low: 3999.0);

            Assert.False(lossLock.IsLocked);
        }

        [Fact]
        public void a_win_clears_the_streak_and_unlocks() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedTrade(Win, EntryAtr, Anchor);

            Assert.Equal(0, lossLock.ConsecutiveLosses);
            Assert.False(lossLock.IsLocked);
        }

        // 与交易 CSV 的「盈利/亏损」口径一致：持平记为盈利。
        [Fact]
        public void a_breakeven_trade_clears_the_streak() {
            ConsecutiveLossLock lossLock = CreateLock();

            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);
            lossLock.RecordClosedTrade(0.0, EntryAtr, Anchor);

            Assert.Equal(0, lossLock.ConsecutiveLosses);
        }

        // 解锁后那一单又亏，锚点和参照 ATR 都换成最新那笔的。
        [Fact]
        public void relocking_uses_the_latest_losing_trade() {
            ConsecutiveLossLock lossLock = CreateLockedLock();
            lossLock.RecordClosedBar(high: 4031.0, low: 4025.0);

            lossLock.RecordClosedTrade(Loss, 5.0, 4200.0);

            Assert.True(lossLock.IsLocked);
            Assert.Equal(5.0, lossLock.LockedAtr);
            Assert.Equal(4200.0, lossLock.AnchorPrice);
            Assert.Equal(15.0, lossLock.RequiredDistance);
        }

        [Fact]
        public void does_not_lock_when_nlock_is_zero() {
            ConsecutiveLossLock lossLock = new(maxConsecutiveLosses: 0, maxBarRangeAtr: BarRangeMultiple);

            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);
            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);
            lossLock.RecordClosedTrade(Loss, EntryAtr, Anchor);

            Assert.False(lossLock.IsEnabled);
            Assert.False(lossLock.IsLocked);
        }

        // 冻结 ATR 缺失时不该无限期锁死：下一根 K 线就放开。
        [Fact]
        public void unlocks_on_the_next_bar_when_the_frozen_atr_is_missing() {
            ConsecutiveLossLock lossLock = CreateLock();
            lossLock.RecordClosedTrade(Loss, 0.0, Anchor);
            lossLock.RecordClosedTrade(Loss, 0.0, Anchor);
            Assert.True(lossLock.IsLocked);

            lossLock.RecordClosedBar(high: 4000.5, low: 4000.0);

            Assert.False(lossLock.IsLocked);
        }

        // 用截图那一笔的真实数据回归：入场 4063.52、冻结 ATR 11.6129、倍数 3 -> 需跑出 34.84。
        // 山峰期间最大单根振幅只有 17.04（旧规则永不解锁），但 10:15 那根 High 4099.35 离锚点 35.83。
        [Fact]
        public void unlocks_on_the_real_s_pin_2_hill() {
            ConsecutiveLossLock lossLock = new(maxConsecutiveLosses: 1, maxBarRangeAtr: 3.0);
            lossLock.RecordClosedTrade(Loss, 11.612863001579807, 4063.52);
            Assert.True(lossLock.IsLocked);

            lossLock.RecordClosedBar(high: 4089.75, low: 4072.71); // 振幅 17.04，离锚点 26.23
            Assert.True(lossLock.IsLocked);

            lossLock.RecordClosedBar(high: 4099.35, low: 4085.66); // 振幅 13.69，离锚点 35.83

            Assert.False(lossLock.IsLocked);
        }
    }
}
