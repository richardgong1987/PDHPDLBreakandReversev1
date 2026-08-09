using System;

namespace cAlgo.Robots;

public static class RmaUtils {
    public static RmaPositionModel GetFastToSlowPosition(double fastRma, double slowRma) {
        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) ||
            double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            throw new ArgumentException("RMA values must be finite numbers.");

        if (fastRma < slowRma)
            return RmaPositionModel.FastBelowSlow;

        if (fastRma > slowRma)
            return RmaPositionModel.FastAboveSlow;

        return RmaPositionModel.Equal;
    }
}
