namespace cAlgo.Robots;

// The subset of cTrader symbol facts the planner needs, expressed without any
// cAlgo.API type. CAlgoSymbol adapts the real Symbol; tests supply a fake.
public interface IPdhpdlSymbolModel {
    double TickSize { get; }
    double PipSize { get; }
    double LotSize { get; }
    double VolumeInUnitsMin { get; }
    double VolumeInUnitsMax { get; }

    double NormalizeVolumeInUnits(double volumeInUnits);
    double VolumeForProportionalRisk(double riskPct, double stopLossPips);
    double AmountRisked(double volumeInUnits, double stopLossPips);
}
