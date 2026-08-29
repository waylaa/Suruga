namespace Suruga.PostProcessing.Extensions;

internal static class DoubleExtensions
{
    private const double ComparisonTolerance = 1e-9;
    
    internal static bool IsApproximatelyEqualTo(this double value, double other)
        => Math.Abs(value - other) < ComparisonTolerance;
}
