namespace Suruga.PostProcessing.Wsola.Converters;

internal static class MonoConverter
{
    internal static float[] Convert(ReadOnlySpan<float> samples, int channels)
    {
        int frameCount = samples.Length / channels;
        float[] mono = GC.AllocateUninitializedArray<float>(frameCount);

        if (channels == 2)
        {
            for (int frame = 0, sample = 0; frame < frameCount; frame++, sample += 2)
            {
                mono[frame] = (samples[sample] + samples[sample + 1]) * 0.5f;
            }
            
            return mono;
        }

        float inverseChannels = 1f / channels;
        
        for (int frame = 0; frame < frameCount; frame++)
        {
            int sample = frame * channels;
            float sum = 0;

            for (int channel = 0; channel < channels; channel++)
            {
                sum += samples[sample + channel];
            }
            
            mono[frame] = sum * inverseChannels;
        }

        return mono;
    }
}
