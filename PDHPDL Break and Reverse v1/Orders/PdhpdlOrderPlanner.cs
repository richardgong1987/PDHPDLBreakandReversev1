using System;

namespace cAlgo.Robots;

// Turns a signal into a sized, validated order plan. Pure: it depends only on the
// IPdhpdlSymbolModel port and PdhpdlRiskGuard, never on cAlgo, so it is unit tested.
//
// Sizing: volume = riskMoney / riskPrice, rounded to the nearest tradable step, so a
// stop-out loses as close to the risk budget (e.g. 1% of equity) as the step allows.
public class PdhpdlOrderPlanner {
    public const string LabelPrefix = "PDHPDL_V1";

    private readonly IPdhpdlSymbolModel _symbolModel;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly int _stopOffsetTicks;
    private readonly double _takeProfitR;
    private readonly PdhpdlEntryModel _entryModel;
    private readonly double _riskPct;

    public PdhpdlOrderPlanner(IPdhpdlSymbolModel symbolModel, PdhpdlRiskGuard riskGuard, int stopOffsetTicks, double takeProfitR,
        PdhpdlEntryModel entryModel, double riskPct) {
        _symbolModel = symbolModel;
        _riskGuard = riskGuard;
        _stopOffsetTicks = stopOffsetTicks;
        _takeProfitR = takeProfitR;
        _entryModel = entryModel;
        _riskPct = riskPct;
    }

    public PdhpdlOrderPlanModel CreatePlan(PdhpdlSignal signal, double accountEquity) {
        PdhpdlOrderPlanModel planModel = new();

        PdhpdlTradeDirectionModel directionModel = signal.IsLongSignal ? PdhpdlTradeDirectionModel.Long : PdhpdlTradeDirectionModel.Short;
        FillGeometry(signal, directionModel, out double entry, out double stop, out double riskPrice, out double takeProfit);

        if (_riskGuard.TryGetRiskPriceRejectReason(riskPrice, out string rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        double stopLossPips = riskPrice / _symbolModel.PipSize;
        double takeProfitPips = Math.Abs(takeProfit - entry) / _symbolModel.PipSize;
        double riskMoney = _riskGuard.CalculateRiskMoney(accountEquity, _riskPct);

        // Volume whose loss at the stop equals the risk budget (units * riskPrice = riskMoney),
        // snapped to the nearest tradable step so the realized loss lands as close to the budget
        // as the step allows. Rounding down here (the old behavior) systematically under-risked,
        // badly so for small positions; nearest rounding centers realized risk on the budget.
        double idealVolume = riskMoney / riskPrice;
        double totalVolume = _symbolModel.NormalizeVolumeInUnits(idealVolume);

        if (TryGetVolumeRejectReason(totalVolume, out rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        FillPlan(planModel, directionModel, entry, stop, takeProfit, riskPrice, stopLossPips, takeProfitPips, totalVolume,
            accountEquity, riskMoney);
        return planModel;
    }

    private void FillGeometry(PdhpdlSignal signal, PdhpdlTradeDirectionModel directionModel, out double entry, out double stop,
        out double riskPrice, out double takeProfit) {
        double closeEntry = signal.Close;
        double stopOffset = _symbolModel.TickSize * _stopOffsetTicks;

        if (directionModel == PdhpdlTradeDirectionModel.Long) {
            stop = signal.Low - stopOffset;
            entry = GetEntryPrice(closeEntry, stop, directionModel);
            riskPrice = entry - stop;
            takeProfit = entry + _takeProfitR * riskPrice;
        } else {
            stop = signal.High + stopOffset;
            entry = GetEntryPrice(closeEntry, stop, directionModel);
            riskPrice = stop - entry;
            takeProfit = entry - _takeProfitR * riskPrice;
        }
    }

    private bool TryGetVolumeRejectReason(double totalVolume, out string rejectReason) {
        rejectReason = "";

        if (totalVolume < _symbolModel.VolumeInUnitsMin) {
            rejectReason = $"Calculated volume is below broker minimum. TotalVolume={totalVolume}, Min={_symbolModel.VolumeInUnitsMin}";
            return true;
        }

        if (totalVolume > _symbolModel.VolumeInUnitsMax) {
            rejectReason = $"Calculated volume is above broker maximum. TotalVolume={totalVolume}, Max={_symbolModel.VolumeInUnitsMax}";
            return true;
        }

        return false;
    }

    private void FillPlan(PdhpdlOrderPlanModel planModel, PdhpdlTradeDirectionModel directionModel, double entry, double stop, double takeProfit,
        double riskPrice, double stopLossPips, double takeProfitPips, double totalVolume, double accountEquity, double riskMoney) {
        string side = directionModel == PdhpdlTradeDirectionModel.Long ? "L" : "S";

        planModel.IsValid = true;
        planModel.DirectionModel = directionModel;
        planModel.EntryModel = _entryModel;
        planModel.IsMarketOrder = _entryModel == PdhpdlEntryModel.Close;
        planModel.EntryPrice = entry;
        planModel.StopPrice = stop;
        planModel.TakeProfitPrice = takeProfit;
        planModel.RiskPrice = riskPrice;
        planModel.StopLossPips = stopLossPips;
        planModel.TakeProfitPips = takeProfitPips;
        planModel.TotalLots = totalVolume / _symbolModel.LotSize;
        planModel.TotalVolumeInUnits = totalVolume;
        planModel.AccountEquity = accountEquity;
        planModel.RiskMoney = riskMoney;
        planModel.EstimatedRiskMoney = _symbolModel.AmountRisked(totalVolume, stopLossPips);
        planModel.Label = $"{LabelPrefix}_{side}";
    }

    private double GetEntryPrice(double closeEntry, double stop, PdhpdlTradeDirectionModel directionModel) {
        double ratio = GetPullbackRatio();

        if (ratio <= 0.0)
            return closeEntry;

        double distanceToStop = Math.Abs(closeEntry - stop);

        if (directionModel == PdhpdlTradeDirectionModel.Long)
            return closeEntry - distanceToStop * ratio;

        return closeEntry + distanceToStop * ratio;
    }

    private double GetPullbackRatio() {
        switch (_entryModel) {
            case PdhpdlEntryModel.Pullback25:
                return 0.25;
            case PdhpdlEntryModel.Pullback382:
                return 0.382;
            case PdhpdlEntryModel.Pullback50:
                return 0.50;
            default:
                return 0.0;
        }
    }
}
