using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.OrderLogger {
    // Locks in the "多空"(Side) column removal: the migrator must upgrade files written by the
    // previous (with-Side) schema to the current 22-column schema, stripping column index 1.
    public class PdhpdlTradeCsvMigratorTests {
        // 移除 "多空" 之前本 cBot 输出的表头（23 列，Side 位于索引 1）。
        private const string PreviousHeaderWithSide =
            "编号,多空,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";

        // 当前表头（22 列，已无 "多空"）。与 PdhpdlTradeCsvLogger.BuildHeader 保持一致。
        private const string CurrentHeader =
            "编号,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";

        [Fact]
        public void strips_side_column_when_upgrading_previous_with_side_file() {
            string rowWithSide = "1001,B,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001";
            string[] lines = { PreviousHeaderWithSide, rowWithSide };

            string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, CurrentHeader);

            string expectedRow = "1001,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001";
            Assert.Equal(CurrentHeader, upgraded[0]);
            Assert.Equal(expectedRow, upgraded[1]);
            Assert.Equal(22, upgraded[1].Split(',').Length);
        }

        [Fact]
        public void returns_null_when_file_is_already_on_current_schema() {
            string currentRow = "1001,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001";
            string[] lines = { CurrentHeader, currentRow };

            string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.Null(upgraded);
        }
    }
}
