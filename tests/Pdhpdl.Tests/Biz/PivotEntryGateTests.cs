using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.Biz {
    // 令牌闸门：每一笔作多要吃掉一个新的 HH，每一笔作空要吃掉一个新的 LL，多空各记各的。
    public class PivotEntryGateTests {
        private const PdhpdlTradeDirectionModel Short = PdhpdlTradeDirectionModel.Short;
        private const PdhpdlTradeDirectionModel Long = PdhpdlTradeDirectionModel.Long;

        private const MarketStructurePivotModel LowerLow = MarketStructurePivotModel.LowerLow;
        private const MarketStructurePivotModel LowerHigh = MarketStructurePivotModel.LowerHigh;
        private const MarketStructurePivotModel HigherHigh = MarketStructurePivotModel.HigherHigh;
        private const MarketStructurePivotModel HigherLow = MarketStructurePivotModel.HigherLow;
        private const MarketStructurePivotModel NoPivot = MarketStructurePivotModel.None;

        // 第一笔也要有自己的令牌：还没出结构点就不许开仓。
        [Fact]
        public void first_entry_still_needs_its_own_pivot() {
            var gate = new PivotEntryGate();

            Assert.False(gate.IsAllowed(Short, NoPivot, pivotCount: 0));
            Assert.False(gate.IsAllowed(Long, NoPivot, pivotCount: 0));
            Assert.True(gate.IsAllowed(Long, HigherHigh, pivotCount: 1));
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 1));
        }

        // LH 虽然也是红色标记，但它只是回调里的次级高点，不算趋势又走远了一步。
        [Theory]
        [InlineData(LowerHigh)]
        [InlineData(HigherHigh)]
        [InlineData(HigherLow)]
        [InlineData(NoPivot)]
        public void short_is_blocked_unless_the_pivot_is_a_lower_low(MarketStructurePivotModel latestPivot) {
            var gate = new PivotEntryGate();

            Assert.False(gate.IsAllowed(Short, latestPivot, pivotCount: 4));
        }

        [Theory]
        [InlineData(HigherLow)]
        [InlineData(LowerLow)]
        [InlineData(LowerHigh)]
        [InlineData(NoPivot)]
        public void long_is_blocked_unless_the_pivot_is_a_higher_high(MarketStructurePivotModel latestPivot) {
            var gate = new PivotEntryGate();

            Assert.False(gate.IsAllowed(Long, latestPivot, pivotCount: 4));
        }

        // 令牌语义：种类对了也不够，结构点没更新就不能再用。
        [Fact]
        public void a_lower_low_is_spent_by_the_short_it_lets_through() {
            var gate = new PivotEntryGate();

            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 4));
            gate.RecordEntry(Short, pivotCount: 4);
            Assert.False(gate.IsAllowed(Short, LowerLow, pivotCount: 4));

            // 下一笔要等一个新的 LL。
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 5));
        }

        [Fact]
        public void a_higher_high_is_spent_by_the_long_it_lets_through() {
            var gate = new PivotEntryGate();

            Assert.True(gate.IsAllowed(Long, HigherHigh, pivotCount: 4));
            gate.RecordEntry(Long, pivotCount: 4);
            Assert.False(gate.IsAllowed(Long, HigherHigh, pivotCount: 4));

            Assert.True(gate.IsAllowed(Long, HigherHigh, pivotCount: 5));
        }

        // 中间夹了别的结构点也没关系：只要最后那个是自己要的种类，而且比上一笔入场时新。
        [Fact]
        public void an_unusable_pivot_in_between_does_not_block_the_next_lower_low() {
            var gate = new PivotEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            Assert.False(gate.IsAllowed(Short, LowerHigh, pivotCount: 4));
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 5));
        }

        // 多空各记各的：作多吃掉的 HH 不该把作空手里那个 LL 也算成用过。
        [Fact]
        public void spending_a_higher_high_leaves_the_short_token_untouched() {
            var gate = new PivotEntryGate();
            gate.RecordEntry(Long, pivotCount: 9);

            Assert.False(gate.IsAllowed(Long, HigherHigh, pivotCount: 9));
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 9));
        }

        // 换方向不再是「重新开始」：作空一样要等自己的新 LL。
        [Fact]
        public void switching_direction_does_not_hand_out_a_free_entry() {
            var gate = new PivotEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);
            gate.RecordEntry(Long, pivotCount: 4);

            Assert.False(gate.IsAllowed(Short, LowerLow, pivotCount: 3));
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 4));
        }
    }
}
