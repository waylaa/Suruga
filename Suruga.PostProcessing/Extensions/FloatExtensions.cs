namespace Suruga.PostProcessing.Extensions;

internal static class FloatExtensions
{
    private const float ComparisonTolerance = 1e-9f;
    
    internal static bool IsApproximatelyEqualTo(this float value, float other)
        => Math.Abs(value - other) < ComparisonTolerance;
}
