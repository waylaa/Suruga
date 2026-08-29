using System.Numerics;

namespace Suruga.PostProcessing;

internal static class GainProcessor
{
    internal static void Process(Span<float> samples, double gain)
    {
        int width = Vector<float>.Count;
        int vectorEnd = samples.Length - samples.Length % width;

        float g = (float)gain;
        int i = 0;

        for (; i < vectorEnd; i += width)
        {
            Vector<float> sample = new(samples[i..]);
            sample *= g;
                
            sample.CopyTo(samples[i..]);
        }

        for (; i < samples.Length; i++)
        {
            ref float sample = ref samples[i];
            sample *= g;
        }
    }
}
