namespace Suruga.PostProcessing.Wsola.Synthesis;

internal sealed class OverlapAddSynthesizer
{
    internal int LengthFrames { get; private set; }
    
    private float[] _output = GC.AllocateUninitializedArray<float>(0);
    private float[] _weights = GC.AllocateUninitializedArray<float>(0);

    internal void Add(ReadOnlySpan<float> frame, ReadOnlySpan<float> window, int outputFrame, int channels)
    {
        int frameCount = frame.Length / channels;
        EnsureCapacity(outputFrame + frameCount, channels);

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            float gain = window[frameIndex];

            int sourceIndex = frameIndex * channels;
            int destinationIndex = (outputFrame + frameIndex) * channels;

            for (int channel = 0; channel < channels; channel++)
            {
                _output[destinationIndex + channel] += frame[sourceIndex + channel] * gain;
                _weights[destinationIndex + channel] += gain;
            }
        }

        LengthFrames = Math.Max(LengthFrames, outputFrame + frameCount);
    }

    internal int Read(Span<float> destination, int channels)
    {
        int frames = Math.Min(destination.Length / channels, LengthFrames);

        for (int frame = 0; frame < frames; frame++)
        {
            int index = frame * channels;

            for (int channel = 0; channel < channels; channel++)
            {
                float weight = _weights[index + channel];

                destination[index + channel] = weight > 1e-8f
                    ? _output[index + channel] / weight
                    : 0;
            }
        }
        
        int remainingFrames = LengthFrames - frames;
        int remainingSamples = remainingFrames * channels;
        
        Array.Copy
        (
            _output,
            frames * channels,
            _output,
            0,
            remainingSamples
        );

        Array.Copy
        (
            _weights,
            frames * channels,
            _weights,
            0,
            remainingSamples
        );

        Array.Clear(_output, remainingSamples, frames * channels);
        Array.Clear(_weights, remainingSamples, frames * channels);

        LengthFrames = remainingFrames;
        return frames;
    }

    internal void Clear()
    {
        Array.Clear(_output);
        Array.Clear(_weights);

        LengthFrames = 0;
    }

    private void EnsureCapacity(int requiredFrames, int channels)
    {
        int requiredSamples = requiredFrames * channels;

        if (_output.Length < requiredSamples)
        {
            Array.Resize(ref _output, requiredSamples);
        }

        if (_weights.Length < requiredSamples)
        {
            Array.Resize(ref _weights, requiredSamples);
        }
    }
}
