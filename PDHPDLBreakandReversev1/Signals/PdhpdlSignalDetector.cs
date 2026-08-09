using System;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;
    private readonly DualRmaSeries _rmaSeries;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars, DualRmaSeries rmaSeries) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
        _rmaSeries = rmaSeries;
    }

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy, ParameterModel parameter) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;

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

        signalModel.Pdh1 = parameter.Pdh1;
        signalModel.Pdh2 = parameter.Pdh2;
        signalModel.Pdh3 = parameter.Pdh3;
        signalModel.Pdh4 = parameter.Pdh4;
        signalModel.Pdh5 = parameter.Pdh5;

        signalModel.Pdl1 = parameter.Pdl1;
        signalModel.Pdl2 = parameter.Pdl2;
        signalModel.Pdl3 = parameter.Pdl3;
        signalModel.Pdl4 = parameter.Pdl4;
        signalModel.Pdl5 = parameter.Pdl5;

        FillRmaData(signalModel);

        MainBiz.Evaluate(signalModel, current, previous, earlier);

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
}
