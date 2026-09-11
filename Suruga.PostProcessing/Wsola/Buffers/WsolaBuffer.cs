namespace Suruga.PostProcessing.Wsola.Buffers;

internal sealed class WsolaBuffer
{
    internal ReadOnlySpan<float> Samples => _buffer.AsSpan(0, LengthFrames * _channels);
    
    internal int LengthFrames { get; private set; }

    private readonly int _channels;

    private float[] _buffer = GC.AllocateUninitializedArray<float>(0);
    
    internal WsolaBuffer(int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _channels = channels;
    }

    internal void Append(ReadOnlySpan<float> samples)
    {
        int appendFrames = samples.Length / _channels;
        int requiredSamples = (LengthFrames + appendFrames) * _channels;

        if (_buffer.Length < requiredSamples)
        {
            Array.Resize(ref _buffer, requiredSamples);
        }

        samples.CopyTo(_buffer.AsSpan(LengthFrames * _channels));
        LengthFrames += appendFrames;
    }

    internal void Discard(int frames)
    {
        if (frames <= 0)
        {
            return;
        }

        frames = Math.Min(frames, LengthFrames);

        int remainingFrames = LengthFrames - frames;
        int remainingSamples = remainingFrames * _channels;

        Array.Copy(_buffer, frames * _channels, _buffer, 0, remainingSamples);
        Array.Clear(_buffer, remainingSamples, frames * _channels);
        
        LengthFrames = remainingFrames;
    }

    internal void Clear()
        => LengthFrames = 0;
}
