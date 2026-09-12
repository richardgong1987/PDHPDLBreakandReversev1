using System.Linq;
using cAlgo.Robots;
using Xunit;

namespace Pdhpdl.Tests.OrderLogger {
    // Locks in the schema changes the migrator still has to undo for old files: the "多空"(Side)
    // column removal (strip column index 1), and the market-state columns appended since — these
    // only ever grew at the end, so an old row just needs the missing fields as empties.
    public class PdhpdlTradeCsvMigratorTests {
        private const int CurrentColumnCount = 30;

        // 移除 "多空" 之前本 cBot 输出的表头（23 列，Side 位于索引 1）。
        private const string PreviousHeaderWithSide =
            "编号,多空,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";

        // 依次是历史上每次 "在末尾追加列" 之前的表头，最后一个是当前表头。
        // 与 PdhpdlTradeCsvLogger.BuildHeader 保持一致。
        private const string HeaderBeforeAtrState =
            "编号,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";
        private const string HeaderBeforeDmsState = HeaderBeforeAtrState + ",ATR_Ratio_H1,PD_Range_ATR";
        private const string HeaderBeforeAdxPreviousState = HeaderBeforeDmsState + ",ADX14_H1,DI+14_H1,DI-14_H1";
        private const string HeaderBeforeGapX = HeaderBeforeAdxPreviousState + ",ADX14_H1_Previous";
        // GapX_3Bar 这一列在 GapX_1Bar 加入之前叫 GapX；位置没变，只是改了名。
        private const string HeaderBeforeShortGapX = HeaderBeforeGapX + ",GapX";
        private const string CurrentHeader = HeaderBeforeGapX + ",GapX_3Bar,GapX_1Bar";

        private const string CurrentRow =
            "1001,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001,1.2345,0.87,28.4,31.2,12.9,26.1,-0.32,-0.11";

        // 每个曾经的表头配它当时的列数。24 列的布局与一个更早的历史布局列数相同，靠表头区分。
        [Theory]
        [InlineData(HeaderBeforeAtrState, 22)]
        [InlineData(HeaderBeforeDmsState, 24)]
        [InlineData(HeaderBeforeAdxPreviousState, 27)]
        [InlineData(HeaderBeforeGapX, 28)]
        [InlineData(HeaderBeforeShortGapX, 29)]
        public void pads_the_columns_appended_after_a_row_was_written(string previousHeader, int previousColumnCount) {
            string previousRow = string.Join(",", CurrentRow.Split(',').Take(previousColumnCount));
            string[] lines = { previousHeader, previousRow };

            string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.Equal(CurrentHeader, upgraded[0]);
            Assert.Equal(previousRow + new string(',', CurrentColumnCount - previousColumnCount), upgraded[1]);
        }

        [Fact]
        public void strips_side_column_when_upgrading_previous_with_side_file() {
            string rowWithSide = "1001,B,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001";
            string[] lines = { PreviousHeaderWithSide, rowWithSide };

            string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, CurrentHeader);

            string expectedRow =
                "1001,PDL,false-breakout,收线入场,ENTRY,XAUUSD,m5,2026-01-01 00:00:00,2000,,1990,2020,10,1,,10000,,0,,,1001,5001,,,,,,,,";
            Assert.Equal(CurrentHeader, upgraded[0]);
            Assert.Equal(expectedRow, upgraded[1]);
            Assert.Equal(CurrentColumnCount, upgraded[1].Split(',').Length);
        }

        [Fact]
        public void returns_null_when_file_is_already_on_current_schema() {
            string[] lines = { CurrentHeader, CurrentRow };

            string[] upgraded = PdhpdlTradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.Null(upgraded);
        }
    }
}
