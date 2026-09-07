namespace cAlgo.Robots;

// 结构点令牌闸门。
//
// 每一笔作多都要吃掉一个新的 HH，每一笔作空都要吃掉一个新的 LL。同为绿色的 HL 和同为红色的
// LH 只是回调里的次级结构点，一律不放行。方向不再分「连续 / 换向」——换方向后的第一笔同样
// 要有自己的令牌。
//
// 「新的」靠 MarketStructure.PivotCount 判断：它只增不减，比这个方向上一笔入场时记下的值大，
// 就说明这中间确实又新确认了一个结构点。只看种类是不够的 —— 结构点不变的那段时间里每个信号
// 都会被放行，等于没有闸门。
//
// 多空各记各的：作多吃掉的 HH 不影响作空手里那个 LL，反过来也一样。
//
// 计数只认真正开出来的仓位（见 PdhpdlOrderExecutor）。信号被风控或「本品种已有持仓」闸门
// 拦掉的不算——按信号计数会把令牌白白用掉。
public class PivotEntryGate {
    private int _pivotCountAtLastLong;
    private int _pivotCountAtLastShort;

    public bool IsAllowed(PdhpdlTradeDirectionModel direction, MarketStructurePivotModel latestPivot, int pivotCount) {
        if (!IsPivotAligned(direction, latestPivot))
            return false;

        return pivotCount > PivotCountAtLastEntry(direction);
    }

    public void RecordEntry(PdhpdlTradeDirectionModel direction, int pivotCount) {
        if (direction == PdhpdlTradeDirectionModel.Long) {
            _pivotCountAtLastLong = pivotCount;
            return;
        }

        _pivotCountAtLastShort = pivotCount;
    }

    private int PivotCountAtLastEntry(PdhpdlTradeDirectionModel direction) {
        return direction == PdhpdlTradeDirectionModel.Long ? _pivotCountAtLastLong : _pivotCountAtLastShort;
    }

    private static bool IsPivotAligned(PdhpdlTradeDirectionModel direction, MarketStructurePivotModel latestPivot) {
        if (direction == PdhpdlTradeDirectionModel.Short) {
            return latestPivot == MarketStructurePivotModel.LowerLow;
        }

        return latestPivot == MarketStructurePivotModel.HigherHigh;
    }
}
