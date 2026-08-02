using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // Nlock = 2，倍数 = 2：上锁后需要一根振幅 > 2 × 冻结ATR 的 K 线才解锁。
    public class ConsecutiveLossLockTests {
        private const int Nlock = 2;
        private const double BarRangeMultiple = 2.0;
        private const double EntryAtr = 8.0;
        private const double Loss = -10.0;
        private const double Win = 20.0;

        private static ConsecutiveLossLock CreateLock() => new(Nlock, BarRangeMultiple);

        private static ConsecutiveLossLock CreateLockedLock() {
            ConsecutiveLossLock lossLock = CreateLock();
            lossLock.RecordClosedTrade(Loss, EntryAtr);
            lossLock.RecordClosedTrade(Loss, EntryAtr);
            return lossLock;
        }

        [Fact]
        public void starts_unlocked() {
            Assert.False(CreateLock().IsLocked);
        }

        [Fact]
        public void stays_unlocked_before_the_streak_reaches_nlock() {
            ConsecutiveLossLock lossLock = CreateLock();

            lossLock.RecordClosedTrade(Loss, EntryAtr);

            Assert.Equal(1, lossLock.ConsecutiveLosses);
            Assert.False(lossLock.IsLocked);
        }

        [Fact]
        public void locks_when_the_streak_reaches_nlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            Assert.True(lossLock.IsLocked);
            Assert.Equal(EntryAtr, lossLock.LockedAtr);
            Assert.Equal(16.0, lossLock.RequiredBarRange);
        }

        [Fact]
        public void a_small_bar_does_not_unlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 106.0, low: 100.0); // 振幅 6 < 16

            Assert.True(lossLock.IsLocked);
        }

        // 门槛是严格大于：正好等于 2 × ATR 不算走出来。
        [Fact]
        public void a_bar_exactly_at_the_threshold_does_not_unlock() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 116.0, low: 100.0); // 振幅 16 == 16

            Assert.True(lossLock.IsLocked);
        }

        [Fact]
        public void a_big_enough_bar_unlocks() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 117.0, low: 100.0); // 振幅 17 > 16

            Assert.False(lossLock.IsLocked);
        }

        // 解锁是一次性的：解开之后再小的 K 线也不会重新锁上。
        [Fact]
        public void stays_unlocked_after_later_small_bars() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedBar(high: 117.0, low: 100.0);
            lossLock.RecordClosedBar(high: 101.0, low: 100.0);
            lossLock.RecordClosedBar(high: 102.0, low: 100.0);

            Assert.False(lossLock.IsLocked);
        }

        [Fact]
        public void a_win_clears_the_streak_and_unlocks() {
            ConsecutiveLossLock lossLock = CreateLockedLock();

            lossLock.RecordClosedTrade(Win, EntryAtr);

            Assert.Equal(0, lossLock.ConsecutiveLosses);
            Assert.False(lossLock.IsLocked);
        }

        // 与交易 CSV 的「盈利/亏损」口径一致：持平记为盈利。
        [Fact]
        public void a_breakeven_trade_clears_the_streak() {
            ConsecutiveLossLock lossLock = CreateLock();

            lossLock.RecordClosedTrade(Loss, EntryAtr);
            lossLock.RecordClosedTrade(0.0, EntryAtr);

            Assert.Equal(0, lossLock.ConsecutiveLosses);
        }

        // 解锁后那一单又亏，参照 ATR 换成最新那笔的。
        [Fact]
        public void relocking_uses_the_latest_losing_entry_atr() {
            ConsecutiveLossLock lossLock = CreateLockedLock();
            lossLock.RecordClosedBar(high: 117.0, low: 100.0);

            lossLock.RecordClosedTrade(Loss, 5.0);

            Assert.True(lossLock.IsLocked);
            Assert.Equal(5.0, lossLock.LockedAtr);
            Assert.Equal(10.0, lossLock.RequiredBarRange);
        }

        [Fact]
        public void does_not_lock_when_nlock_is_zero() {
            ConsecutiveLossLock lossLock = new(maxConsecutiveLosses: 0, maxBarRangeAtr: BarRangeMultiple);

            lossLock.RecordClosedTrade(Loss, EntryAtr);
            lossLock.RecordClosedTrade(Loss, EntryAtr);
            lossLock.RecordClosedTrade(Loss, EntryAtr);

            Assert.False(lossLock.IsEnabled);
            Assert.False(lossLock.IsLocked);
        }

        // 冻结 ATR 缺失时不该无限期锁死：下一根 K 线就放开。
        [Fact]
        public void unlocks_on_the_next_bar_when_the_frozen_atr_is_missing() {
            ConsecutiveLossLock lossLock = CreateLock();
            lossLock.RecordClosedTrade(Loss, 0.0);
            lossLock.RecordClosedTrade(Loss, 0.0);
            Assert.True(lossLock.IsLocked);

            lossLock.RecordClosedBar(high: 100.5, low: 100.0);

            Assert.False(lossLock.IsLocked);
        }
    }
}
