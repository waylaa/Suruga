namespace Suruga.PostProcessing.Wsola.Helpers;

internal static class CorrelationHelper
{
    internal static ReadOnlySpan<float> BuildPrefixSumOfSquares(ReadOnlySpan<float> values)
    {
        Span<float> prefix = GC.AllocateUninitializedArray<float>(values.Length + 1);

        for (int i = 0; i < values.Length; i++)
        {
            float value = values[i];
            prefix[i + 1] = MathF.FusedMultiplyAdd(value, value, prefix[i]);
        }

        return prefix;
    }
}
