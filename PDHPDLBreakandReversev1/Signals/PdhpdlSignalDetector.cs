using System;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;
    private readonly DualRmaSeries _rmaSeries;
    private readonly MarketStructure _marketStructure;
    private readonly PivotEntryGate _entryGate;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars, DualRmaSeries rmaSeries, MarketStructure marketStructure,
        PivotEntryGate entryGate) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
        _rmaSeries = rmaSeries;
        _marketStructure = marketStructure;
        _entryGate = entryGate;
    }

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;

        if (_chartBars.Count < 2 || !TryGetPreviousDayLevels(out double pdh, out double pdl))
            return signalModel;

        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()
        CandleModel current = ReadCandle(closedBarIndex);
        CandleModel previous = ReadCandle(closedBarIndex - 1);
        CandleModel earlier = ReadCandle(closedBarIndex - 2);

        signalModel.HasData = true;
        signalModel.BarIndex = closedBarIndex;
        signalModel.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signalModel.Open = current.Open;
        signalModel.Close = current.Close;
        signalModel.High = current.High;
        signalModel.Low = current.Low;

        signalModel.Pdh1 = pdh;

        signalModel.Pdl1 = pdl;

        signalModel.LatestPivot = _marketStructure.LatestPivot;
        signalModel.PivotCount = _marketStructure.PivotCount;

        FillRmaData(signalModel);

        MainBiz.Evaluate(signalModel, current, previous, earlier, _entryGate);

        return signalModel;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }

    private void FillRmaData(PdhpdlSignalModel signalModel) {
        signalModel.FastRma = double.NaN;
        signalModel.SlowRma = double.NaN;

        if (!_rmaSeries.TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma))
            return;

        signalModel.HasRmaData = true;
        signalModel.RmaSourceBarTime = sourceBarTime;
        signalModel.FastRma = fastRma;
        signalModel.SlowRma = slowRma;
    }

    private bool TryGetPreviousDayLevels(out double pdh, out double pdl) {
        pdh = double.NaN;
        pdl = double.NaN;

        if (_dailyBars == null || _dailyBars.Count < 2)
            return false;

        int previousDailyIndex = _dailyBars.Count - 2;
        pdh = _dailyBars.HighPrices[previousDailyIndex];
        pdl = _dailyBars.LowPrices[previousDailyIndex];
        return true;
    }
}
