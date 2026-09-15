using System;

namespace cAlgo.Robots;

// Turns a signal into a sized, validated order plan. Pure: it depends only on the
// IPdhpdlSymbolModel port and PdhpdlRiskGuard, never on cAlgo, so it is unit tested.
//
// Sizing: volume = riskMoney / riskPrice, rounded to the nearest tradable step, so a
// stop-out loses as close to the risk budget (e.g. 1% of equity) as the step allows.
public class PdhpdlOrderPlanner {
    private readonly IPdhpdlSymbolModel _symbolModel;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly int _stopOffsetTicks;
    private readonly double _takeProfitR;
    private PdhpdlEntryModel _entryModel;
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

    public void UpdateEntryMode(PdhpdlEntryModel entryModel) {
        _entryModel = entryModel;
    }

    public PdhpdlOrderPlanModel CreatePlan(PdhpdlSignalModel signalModel, double accountEquity) {
        PdhpdlOrderPlanModel planModel = new();

        PdhpdlTradeDirectionModel directionModel =
            signalModel.IsLongSignal ? PdhpdlTradeDirectionModel.Long : PdhpdlTradeDirectionModel.Short;
        PdhpdlEntryModel entryModel = ResolveEntryModel(signalModel);
        FillGeometry(signalModel, directionModel, out double entry, out double stop, out double riskPrice, out double takeProfit);
        double stopLossPips = riskPrice / _symbolModel.PipSize;

        if (_riskGuard.TryGetStopLossPipsRejectReason(stopLossPips, out string rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        double takeProfitPips = Math.Abs(takeProfit - entry) / _symbolModel.PipSize;
        double riskMoney = _riskGuard.CalculateRiskMoney(accountEquity, _riskPct);

        // Volume whose loss at the stop equals the risk budget, snapped to the nearest tradable
        // step. lossPerUnit uses PipValue (account-currency value of a pip), so the budget stays
        // in the account currency. Dividing riskMoney by the raw price distance (the old formula)
        // ignored the quote->deposit currency conversion — e.g. a EUR account trading USD-quoted
        // XAUUSD was sized ~15% too small, so realized losses fell short of the budget.
        double lossPerUnit = stopLossPips * _symbolModel.PipValue;
        double idealVolume = lossPerUnit > 0.0 ? riskMoney / lossPerUnit : 0.0;
        double volume = _symbolModel.NormalizeVolumeInUnits(idealVolume);

        if (TryGetVolumeRejectReason(volume, out rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        FillPlan(planModel, directionModel, entryModel, entry, stop, takeProfit, riskPrice, stopLossPips, takeProfitPips, volume,
            accountEquity, riskMoney);
        return planModel;
    }

    // 大 K 线（振幅 > 3×ATR14）不追收盘价：一律改成挂单，等价格回撤到 GetEntryPrice 算出的中点。
    private PdhpdlEntryModel ResolveEntryModel(PdhpdlSignalModel signalModel) {
        return signalModel.IsBigK ? PdhpdlEntryModel.Pb50 : _entryModel;
    }

    private void FillGeometry(PdhpdlSignalModel signalModel, PdhpdlTradeDirectionModel directionModel, out double entry, out double stop,
        out double riskPrice, out double takeProfit) {
        double stopOffset = _symbolModel.TickSize * _stopOffsetTicks;

        // Stop base comes straight from the signal's SL price (the pattern-specific level the
        // detector chose), then a directional offset buffers it past that level.
        if (directionModel == PdhpdlTradeDirectionModel.Long) {
            stop = signalModel.SL - stopOffset;
            entry = GetEntryPrice(signalModel, stop, directionModel);
            riskPrice = entry - stop;
            takeProfit = entry + _takeProfitR * riskPrice;
        } else {
            stop = signalModel.SL + stopOffset;
            entry = GetEntryPrice(signalModel, stop, directionModel);
            riskPrice = stop - entry;
            takeProfit = entry - _takeProfitR * riskPrice;
        }
    }

    private bool TryGetVolumeRejectReason(double volume, out string rejectReason) {
        rejectReason = "";

        if (volume < _symbolModel.VolumeInUnitsMin) {
            rejectReason = $"Calculated volume is below broker minimum. Volume={volume}, Min={_symbolModel.VolumeInUnitsMin}";
            return true;
        }

        if (volume > _symbolModel.VolumeInUnitsMax) {
            rejectReason = $"Calculated volume is above broker maximum. Volume={volume}, Max={_symbolModel.VolumeInUnitsMax}";
            return true;
        }

        return false;
    }

    private void FillPlan(PdhpdlOrderPlanModel planModel, PdhpdlTradeDirectionModel directionModel, PdhpdlEntryModel entryModel,
        double entry, double stop, double takeProfit, double riskPrice, double stopLossPips, double takeProfitPips, double volume,
        double accountEquity, double riskMoney) {
        planModel.IsValid = true;
        planModel.DirectionModel = directionModel;
        planModel.EntryModel = entryModel;
        planModel.IsMarketOrder = entryModel == PdhpdlEntryModel.Close;
        planModel.EntryPrice = entry;
        planModel.StopPrice = stop;
        planModel.TakeProfitPrice = takeProfit;
        planModel.RiskPrice = riskPrice;
        planModel.StopLossPips = stopLossPips;
        planModel.TakeProfitPips = takeProfitPips;
        planModel.Lots = volume / _symbolModel.LotSize;
        planModel.VolumeInUnits = volume;
        planModel.AccountEquity = accountEquity;
        planModel.RiskMoney = riskMoney;
        planModel.EstimatedRiskMoney = _symbolModel.AmountRisked(volume, stopLossPips);
    }

    private double GetEntryPrice(PdhpdlSignalModel signalModel, double stop, PdhpdlTradeDirectionModel directionModel) {
        // 大 K 线：挂在止损价与该 K 线反向端的中点——多单取最高价、空单取最低价，
        // 也就是回撤到这根大 K 线的一半再进场，而不是追它的收盘价。
        if (signalModel.IsBigK)
            return (stop + GetFavourableExtreme(signalModel, directionModel)) / 2.0;

        double closeEntry = signalModel.Close;
        double ratio = GetPullbackRatio();

        if (ratio <= 0.0)
            return closeEntry;

        double distanceToStop = Math.Abs(closeEntry - stop);

        if (directionModel == PdhpdlTradeDirectionModel.Long)
            return closeEntry - distanceToStop * ratio;

        return closeEntry + distanceToStop * ratio;
    }

    // 顺着盈利方向的那一端：多单在上（最高价），空单在下（最低价）。
    private static double GetFavourableExtreme(PdhpdlSignalModel signalModel, PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? signalModel.High : signalModel.Low;
    }

    private double GetPullbackRatio() {
        switch (_entryModel) {
            case PdhpdlEntryModel.Pb25:
                return 0.25;
            case PdhpdlEntryModel.Pb382:
                return 0.382;
            case PdhpdlEntryModel.Pb50:
                return 0.50;
            default:
                return 0.0;
        }
    }
}
