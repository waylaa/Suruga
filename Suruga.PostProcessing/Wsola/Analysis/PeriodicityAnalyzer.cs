using System.Numerics.Tensors;

namespace Suruga.PostProcessing.Wsola.Analysis;

internal sealed class PeriodicityAnalyzer
{
    private readonly int _minimumPeriod;
    private readonly int _maximumPeriod;

    private const int CoarseStride = 4;

    internal PeriodicityAnalyzer(int minimumPeriod, int maximumPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumPeriod);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumPeriod, minimumPeriod);
        
        _minimumPeriod = minimumPeriod;
        _maximumPeriod = maximumPeriod;
    }

    internal float Analyze(ReadOnlySpan<float> samples)
    {
        if (samples.Length <= _maximumPeriod * 2)
        {
            return 0;
        }

        int available = Math.Min(_maximumPeriod, samples.Length / 2);

        int coarseLag = _minimumPeriod;
        float coarseCorrelation = 0;

        for (int lag = _minimumPeriod; lag <= available; lag += CoarseStride)
        {
            float correlation = Correlate(samples, lag);

            if (correlation > coarseCorrelation)
            {
                coarseCorrelation = correlation;
                coarseLag = lag;
            }
        }
        
        int fineMin = Math.Max(_minimumPeriod, coarseLag - CoarseStride);
        int fineMax = Math.Max(available, coarseLag + CoarseStride);

        float bestCorrelation = coarseCorrelation;

        for (int lag = fineMin; lag <= fineMax; lag++)
        {
            if (lag == coarseLag)
            {
                continue;
            }

            float correlation = Correlate(samples, lag);

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
            }
        }

        return Math.Clamp(bestCorrelation, 0, 1);
    }

    private static float Correlate(ReadOnlySpan<float> samples, int lag)
    {
        int sampleCount = samples.Length - lag;

        ReadOnlySpan<float> reference = samples[..sampleCount];
        ReadOnlySpan<float> delayed = samples.Slice(lag, sampleCount);

        float xy = TensorPrimitives.Dot(reference, delayed);
        float xx = TensorPrimitives.SumOfSquares(reference);
        float yy = TensorPrimitives.SumOfSquares(delayed);

        float denominator = MathF.Sqrt(xx * yy);
        return denominator <= 1e-12f ? 0 : xy / denominator;
    }
}
