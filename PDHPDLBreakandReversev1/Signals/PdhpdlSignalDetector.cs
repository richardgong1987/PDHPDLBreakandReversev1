using System;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;
    private readonly DualRmaSeries _rmaSeries;
    private readonly MarketStructure _marketStructure;
    private readonly PivotEntryGate _entryGate;
    private readonly ConsecutiveLossCounter _lossCounter;
    private readonly GapXSeries _gapXSeries;

    private DateTime _levelsDayOpenTime = DateTime.MinValue;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars, DualRmaSeries rmaSeries, MarketStructure marketStructure,
        PivotEntryGate entryGate, ConsecutiveLossCounter lossCounter, GapXSeries gapXSeries) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
        _rmaSeries = rmaSeries;
        _marketStructure = marketStructure;
        _entryGate = entryGate;
        _lossCounter = lossCounter;
        _gapXSeries = gapXSeries;
    }

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy, BuyOrSellOnlyModel buyOrSellOnly) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;
        signalModel.BuyOrSellOnly = buyOrSellOnly;

        if (_chartBars.Count < 2 || !TryGetPreviousDayLevels(out double pdh, out double pdl))
            return signalModel;

        ResetLossStreakOnNewLevels();

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

        // GapX 是进场条件之一，必须在 Evaluate 之前就位。
        _gapXSeries.Fill(signalModel);

        MainBiz.Evaluate(signalModel, current, previous, earlier, _entryGate, _lossCounter);

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

    // 关键位换到新的一天就把连亏计数清零：亏损是对着昨天那对 PDH/PDL 累出来的，不该带进
    // 新的一对，否则新的一天一开盘结构点闸门就已经被顶开着（见 ConsecutiveLossCounter）。
    // 调用点在关键位就位之后、评估信号之前，所以当天开出来的仓位不会被这里清掉。
    private void ResetLossStreakOnNewLevels() {
        DateTime dayOpenTime = _dailyBars.OpenTimes[PreviousDailyIndex];

        if (dayOpenTime == _levelsDayOpenTime)
            return;

        _levelsDayOpenTime = dayOpenTime;
        _lossCounter.Reset();
    }

    private int PreviousDailyIndex => _dailyBars.Count - 2;

    private bool TryGetPreviousDayLevels(out double pdh, out double pdl) {
        pdh = double.NaN;
        pdl = double.NaN;

        if (_dailyBars == null || _dailyBars.Count < 2)
            return false;

        pdh = _dailyBars.HighPrices[PreviousDailyIndex];
        pdl = _dailyBars.LowPrices[PreviousDailyIndex];
        return true;
    }
}
