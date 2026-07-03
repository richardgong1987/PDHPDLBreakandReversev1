using System;

namespace cAlgo.Robots;

// Turns a signal into a sized, validated order plan. Pure: it depends only on the
// IPdhpdlSymbol port and PdhpdlRiskGuard, never on cAlgo, so it is unit tested.
//
// Sizing takes the smaller of two volumes and never risks more than the budget:
//   - the broker's proportional-risk volume for the stop distance, and
//   - risk money / price risk (a hard cap in account currency).
public class PdhpdlOrderPlanner {
    public const string LabelPrefix = "PDHPDL_V1";

    private readonly IPdhpdlSymbol _symbol;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly int _stopOffsetTicks;
    private readonly double _takeProfitR;
    private readonly PdhpdlEntryMode _entryMode;
    private readonly double _riskPct;

    public PdhpdlOrderPlanner(IPdhpdlSymbol symbol, PdhpdlRiskGuard riskGuard, int stopOffsetTicks, double takeProfitR,
        PdhpdlEntryMode entryMode, double riskPct) {
        _symbol = symbol;
        _riskGuard = riskGuard;
        _stopOffsetTicks = stopOffsetTicks;
        _takeProfitR = takeProfitR;
        _entryMode = entryMode;
        _riskPct = riskPct;
    }

    public PdhpdlOrderPlan CreatePlan(PdhpdlSignal signal, double accountEquity) {
        PdhpdlOrderPlan plan = new();

        PdhpdlTradeDirection direction = signal.IsLongSignal ? PdhpdlTradeDirection.Long : PdhpdlTradeDirection.Short;
        FillGeometry(signal, direction, out double entry, out double stop, out double riskPrice, out double takeProfit);

        if (_riskGuard.TryGetRiskPriceRejectReason(riskPrice, out string rejectReason)) {
            plan.RejectReason = rejectReason;
            return plan;
        }

        double stopLossPips = riskPrice / _symbol.PipSize;
        double takeProfitPips = Math.Abs(takeProfit - entry) / _symbol.PipSize;
        double riskMoney = _riskGuard.CalculateRiskMoney(accountEquity, _riskPct);

        double nativeRiskVolume = _symbol.NormalizeVolumeInUnits(_symbol.VolumeForProportionalRisk(_riskPct, stopLossPips));
        double priceRiskCappedVolume = _symbol.NormalizeVolumeInUnits(riskMoney / riskPrice);
        double totalVolume = _symbol.NormalizeVolumeInUnits(Math.Min(nativeRiskVolume, priceRiskCappedVolume));

        if (TryGetVolumeRejectReason(totalVolume, nativeRiskVolume, priceRiskCappedVolume, riskPrice, riskMoney, out rejectReason)) {
            plan.RejectReason = rejectReason;
            return plan;
        }

        FillPlan(plan, direction, entry, stop, takeProfit, riskPrice, stopLossPips, takeProfitPips, totalVolume,
            nativeRiskVolume, priceRiskCappedVolume, accountEquity, riskMoney);
        return plan;
    }

    private void FillGeometry(PdhpdlSignal signal, PdhpdlTradeDirection direction, out double entry, out double stop,
        out double riskPrice, out double takeProfit) {
        double closeEntry = signal.Close;
        double stopOffset = _symbol.TickSize * _stopOffsetTicks;

        if (direction == PdhpdlTradeDirection.Long) {
            stop = signal.Low - stopOffset;
            entry = GetEntryPrice(closeEntry, stop, direction);
            riskPrice = entry - stop;
            takeProfit = entry + _takeProfitR * riskPrice;
        } else {
            stop = signal.High + stopOffset;
            entry = GetEntryPrice(closeEntry, stop, direction);
            riskPrice = stop - entry;
            takeProfit = entry - _takeProfitR * riskPrice;
        }
    }

    private bool TryGetVolumeRejectReason(double totalVolume, double nativeRiskVolume, double priceRiskCappedVolume,
        double riskPrice, double riskMoney, out string rejectReason) {
        rejectReason = "";

        if (totalVolume < _symbol.VolumeInUnitsMin) {
            rejectReason =
                $"Calculated volume is too small. TotalVolume={totalVolume}, NativeVolume={nativeRiskVolume}, PriceRiskCappedVolume={priceRiskCappedVolume}, Min={_symbol.VolumeInUnitsMin}";
            return true;
        }

        if (totalVolume > _symbol.VolumeInUnitsMax) {
            rejectReason = $"Calculated volume is above broker maximum. TotalVolume={totalVolume}, Max={_symbol.VolumeInUnitsMax}";
            return true;
        }

        double cappedRiskMoney = totalVolume * riskPrice;

        if (cappedRiskMoney > riskMoney) {
            rejectReason =
                $"Calculated volume exceeds risk limit. RiskMoney={riskMoney}, CappedRiskMoney={cappedRiskMoney}, TotalVolume={totalVolume}";
            return true;
        }

        return false;
    }

    private void FillPlan(PdhpdlOrderPlan plan, PdhpdlTradeDirection direction, double entry, double stop, double takeProfit,
        double riskPrice, double stopLossPips, double takeProfitPips, double totalVolume, double nativeRiskVolume,
        double priceRiskCappedVolume, double accountEquity, double riskMoney) {
        string side = direction == PdhpdlTradeDirection.Long ? "L" : "S";

        plan.IsValid = true;
        plan.Direction = direction;
        plan.EntryMode = _entryMode;
        plan.IsMarketOrder = _entryMode == PdhpdlEntryMode.Close;
        plan.EntryPrice = entry;
        plan.StopPrice = stop;
        plan.TakeProfitPrice = takeProfit;
        plan.RiskPrice = riskPrice;
        plan.StopLossPips = stopLossPips;
        plan.TakeProfitPips = takeProfitPips;
        plan.TotalLots = totalVolume / _symbol.LotSize;
        plan.TotalVolumeInUnits = totalVolume;
        plan.NativeRiskVolumeInUnits = nativeRiskVolume;
        plan.PriceRiskCappedVolumeInUnits = priceRiskCappedVolume;
        plan.AccountEquity = accountEquity;
        plan.RiskMoney = riskMoney;
        plan.EstimatedRiskMoney = _symbol.AmountRisked(totalVolume, stopLossPips);
        plan.Label = $"{LabelPrefix}_{side}";
    }

    private double GetEntryPrice(double closeEntry, double stop, PdhpdlTradeDirection direction) {
        double ratio = GetPullbackRatio();

        if (ratio <= 0.0)
            return closeEntry;

        double distanceToStop = Math.Abs(closeEntry - stop);

        if (direction == PdhpdlTradeDirection.Long)
            return closeEntry - distanceToStop * ratio;

        return closeEntry + distanceToStop * ratio;
    }

    private double GetPullbackRatio() {
        switch (_entryMode) {
            case PdhpdlEntryMode.Pullback25:
                return 0.25;
            case PdhpdlEntryMode.Pullback382:
                return 0.382;
            case PdhpdlEntryMode.Pullback50:
                return 0.50;
            default:
                return 0.0;
        }
    }
}
