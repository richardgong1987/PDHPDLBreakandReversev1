namespace cAlgo.Robots;

// The subset of cTrader symbol facts the planner needs, expressed without any
// cAlgo.API type. CAlgoSymbolModel adapts the real Symbol; tests supply a fake.
public interface IPdhpdlSymbolModel {
    double TickSize { get; }
    double PipSize { get; }
    double LotSize { get; }
    double VolumeInUnitsMin { get; }
    double VolumeInUnitsMax { get; }

    // Snap a raw volume to the nearest tradable step (so sizing lands as close to the
    // risk budget as the step allows, rather than always rounding down).
    double NormalizeVolumeInUnits(double volumeInUnits);
    double AmountRisked(double volumeInUnits, double stopLossPips);
}
