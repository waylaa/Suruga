using System.Numerics.Tensors;

namespace Suruga.PostProcessing.Wsola.Analysis;

internal static class EnergyAnalyzer
{
    internal static float Analyze(ReadOnlySpan<float> mono)
    {
        if (mono.IsEmpty)
        {
            return 0;
        }
        
        float sumSquares = TensorPrimitives.SumOfSquares(mono);
        return MathF.Sqrt(sumSquares / mono.Length);
    }
}
