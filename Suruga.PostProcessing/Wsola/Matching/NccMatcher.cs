using System.Numerics.Tensors;

namespace Suruga.PostProcessing.Wsola.Matching;

internal static class NccMatcher
{
    private const int CoarseStride = 4;
    private const float MinimumConfidence = 0.15f;

    internal static WsolaCandidate FindBest
    (
        ReadOnlySpan<float> input,
        ReadOnlySpan<float> previousFrame,
        int nominalPosition,
        int windowFrames,
        int overlapFrames,
        int searchRadiusFrames,
        int channels
    )
    {
        int previousFrameLength = previousFrame.Length / channels;
        int effectiveOverlap = Math.Min(overlapFrames, previousFrameLength);

        if (effectiveOverlap <= 0)
        {
            return new WsolaCandidate(nominalPosition);
        }

        int referenceOffsetFrames = previousFrameLength - effectiveOverlap;

        ReadOnlySpan<float> reference = previousFrame.Slice(
            referenceOffsetFrames * channels,
            effectiveOverlap * channels);

        float referenceEnergy = TensorPrimitives.SumOfSquares(reference);

        if (referenceEnergy <= 1e-12f)
        {
            return new WsolaCandidate(nominalPosition);
        }

        int inputFrames = input.Length / channels;
        
        int minimumPosition = Math.Max(0, nominalPosition - searchRadiusFrames);
        
        int maximumPosition = Math.Min(
            inputFrames - Math.Max(windowFrames, effectiveOverlap),
            nominalPosition + searchRadiusFrames);

        if (maximumPosition < minimumPosition)
        {
            return new WsolaCandidate(nominalPosition);
        }

        int overlapSamples = effectiveOverlap * channels;

        int coarsePosition = minimumPosition;
        float coarseCorrelation = float.NegativeInfinity;

        for (int position = minimumPosition; position <= maximumPosition; position += CoarseStride)
        {
            float correlation = Correlate
            (
                input,
                reference,
                position * channels,
                overlapSamples,
                referenceEnergy
            );

            if (correlation > coarseCorrelation)
            {
                coarseCorrelation = correlation;
                coarsePosition = position;
            }
        }

        int fineMin = Math.Max(minimumPosition, coarsePosition - CoarseStride);
        int fineMax = Math.Min(maximumPosition, coarsePosition + CoarseStride);

        int bestPosition = coarsePosition;
        float bestCorrelation = coarseCorrelation;

        for (int position = fineMin; position <= fineMax; position++)
        {
            if (position == coarsePosition)
            {
                continue;
            }
            
            float correlation = Correlate
            (
                input,
                reference,
                position * channels,
                overlapSamples,
                referenceEnergy
            );

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestPosition = position;
            }
        }

        if (float.IsNegativeInfinity(bestCorrelation) || bestCorrelation < MinimumConfidence)
        {
            return new WsolaCandidate(nominalPosition);
        }

        return new WsolaCandidate(bestPosition);
    }

    private static float Correlate
    (
        ReadOnlySpan<float> input,
        ReadOnlySpan<float> reference,
        int sampleOffset,
        int length,
        float referenceEnergy
    )
    {
        ReadOnlySpan<float> candidate = input.Slice(sampleOffset, length);

        float dot = TensorPrimitives.Dot(reference, candidate);
        float candidateEnergy = TensorPrimitives.SumOfSquares(candidate);
        float denominator = MathF.Sqrt(referenceEnergy * candidateEnergy);

        return denominator > 1e-12f ? dot / denominator : 0;
    }
}
