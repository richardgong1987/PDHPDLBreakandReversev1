using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// Adapts the real cTrader Symbol to IPdhpdlSymbol. Rounding and risk basis are fixed
// here (round volume down, risk measured against equity) so the port stays cAlgo-free.
public class CAlgoSymbol : IPdhpdlSymbol {
    private readonly Symbol _symbol;

    public CAlgoSymbol(Symbol symbol) {
        _symbol = symbol;
    }

    public double TickSize => _symbol.TickSize;
    public double PipSize => _symbol.PipSize;
    public double LotSize => _symbol.LotSize;
    public double VolumeInUnitsMin => _symbol.VolumeInUnitsMin;
    public double VolumeInUnitsMax => _symbol.VolumeInUnitsMax;

    public double NormalizeVolumeInUnits(double volumeInUnits) {
        return _symbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
    }

    public double VolumeForProportionalRisk(double riskPct, double stopLossPips) {
        return _symbol.VolumeForProportionalRisk(ProportionalAmountType.Equity, riskPct, stopLossPips, RoundingMode.Down);
    }

    public double AmountRisked(double volumeInUnits, double stopLossPips) {
        return _symbol.AmountRisked(volumeInUnits, stopLossPips);
    }
}
