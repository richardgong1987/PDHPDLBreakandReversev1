namespace cAlgo.Robots;

public static class RmaUtils {
    public static bool IsFastBelowSlow(double fastRma, double slowRma) {
        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) ||
            double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            return false;

        return fastRma < slowRma;
    }
}
