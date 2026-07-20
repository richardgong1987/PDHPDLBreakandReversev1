using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz;

public class ConsecutiveKeyLevelOrderLimitTests {
    [Fact]
    public void allows_orders_without_limit_when_max_times_is_zero() {
        ConsecutiveKeyLevelOrderLimit limit = new(0);

        for (int placedOrder = 0; placedOrder < 10; placedOrder++)
            limit.RecordPlacedOrder("PDH");

        Assert.False(limit.HasReachedConsecutiveLimit("PDH"));
    }

    [Fact]
    public void blocks_the_fourth_consecutive_order_on_the_same_key_level_when_max_times_is_three() {
        ConsecutiveKeyLevelOrderLimit limit = new(3);

        for (int placedOrder = 0; placedOrder < 3; placedOrder++) {
            Assert.False(limit.HasReachedConsecutiveLimit("PDH"));
            limit.RecordPlacedOrder("PDH");
        }

        Assert.True(limit.HasReachedConsecutiveLimit("PDH"));
    }

    [Fact]
    public void leaves_the_other_key_level_open_while_one_is_blocked() {
        ConsecutiveKeyLevelOrderLimit limit = new(1);

        limit.RecordPlacedOrder("PDH");

        Assert.True(limit.HasReachedConsecutiveLimit("PDH"));
        Assert.False(limit.HasReachedConsecutiveLimit("PDL"));
    }

    [Fact]
    public void restarts_the_streak_after_an_order_on_the_other_key_level() {
        ConsecutiveKeyLevelOrderLimit limit = new(2);

        limit.RecordPlacedOrder("PDH");
        limit.RecordPlacedOrder("PDH");
        Assert.True(limit.HasReachedConsecutiveLimit("PDH"));

        limit.RecordPlacedOrder("PDL");

        Assert.False(limit.HasReachedConsecutiveLimit("PDH"));
    }

    [Fact]
    public void ignores_signals_without_a_key_level() {
        ConsecutiveKeyLevelOrderLimit limit = new(1);

        limit.RecordPlacedOrder("");

        Assert.False(limit.HasReachedConsecutiveLimit(""));
    }
}
