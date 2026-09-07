namespace Suruga.PostProcessing.Resampling.Buffers;

internal sealed class ResamplerBuffer
{
    internal int AvailableFrames { get; private set; }
    
    private int CapacityFrames => _buffer.Length / _channels;

    private readonly int _channels;
    
    private float[] _buffer;
    private int _offset;

    internal ResamplerBuffer(int initialCapacityFrames, int channels)
    {
        _channels = channels;

        int capacity = Math.Max(2, initialCapacityFrames);
        _buffer = GC.AllocateUninitializedArray<float>(capacity * channels);
    }

    internal void Advance(int frameCount)
    {
        if (frameCount <= 0)
        {
            return;
        }

        frameCount = Math.Min(frameCount, AvailableFrames);
        _offset += frameCount;
        AvailableFrames -= frameCount;

        if (AvailableFrames == 0)
        {
            _offset = 0;
        }
    }

    internal void Write(ReadOnlySpan<float> source)
    {
        int framesToWrite = source.Length / _channels;

        if (framesToWrite == 0)
        {
            return;
        }

        EnsureCapacity(AvailableFrames + framesToWrite);

        if (_offset > 0 && _offset + AvailableFrames + framesToWrite > CapacityFrames)
        {
            Array.Copy(_buffer, _offset * _channels, _buffer, 0, AvailableFrames * _channels);
            _offset = 0;
        }

        int destinationStart = (_offset + AvailableFrames) * _channels;
        source.CopyTo(_buffer.AsSpan(destinationStart, source.Length));

        AvailableFrames += framesToWrite;
    }

    internal Span<float> Peek(int startFrame, int frameCount)
    {
        if (startFrame < 0 || frameCount < 0 || startFrame + frameCount > AvailableFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        if (frameCount == 0)
        {
            return Span<float>.Empty;
        }

        int startIndex = (_offset + startFrame) * _channels;
        return _buffer.AsSpan(startIndex, frameCount * _channels);
    }

    internal void Clear()
    {
        _offset = 0;
        AvailableFrames = 0;
    }

    private void EnsureCapacity(int requiredFrames)
    {
        if (requiredFrames <= CapacityFrames)
        {
            return;
        }

        int newCapacity = Math.Max(requiredFrames, CapacityFrames * 2);
        float[] newBuffer = GC.AllocateUninitializedArray<float>(newCapacity * _channels);

        if (AvailableFrames > 0)
        {
            Array.Copy(_buffer, _offset * _channels, newBuffer, 0, AvailableFrames * _channels);
        }

        _buffer = newBuffer;
        _offset = 0;
    }
}
