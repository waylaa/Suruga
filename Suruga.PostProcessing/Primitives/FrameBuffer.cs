using System.Buffers;

namespace Suruga.PostProcessing.Primitives;

internal sealed class FrameBuffer : IDisposable
{
    internal int FrameCount { get; private set; }
    
    internal bool IsEmpty => FrameCount == 0;

    private float[] _buffer;
    private bool _isDisposed;
    
    private const int Channels = 2;

    internal FrameBuffer(int initialCapacityFrames = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacityFrames);
        _buffer = ArrayPool<float>.Shared.Rent(initialCapacityFrames * Channels);
    }
    
    internal void Append(ReadOnlySpan<float> samples)
    {
        ThrowIfDisposed();
 
        if (samples.Length == 0)
        {
            return;
        }
 
        if (samples.Length % Channels != 0)
        {
            throw new ArgumentException("The sample count must be divisible by the channel count.", nameof(samples));
        }
 
        int frames = samples.Length / Channels;
 
        EnsureCapacity(FrameCount + frames);
 
        samples.CopyTo(_buffer.AsSpan(FrameCount * Channels));
        FrameCount += frames;
    }
 
    /// <summary>
    /// Drops the first <paramref name="frames"/> frames, compacting
    /// whatever remains to the front so the buffer is always contiguous
    /// starting at index 0.
    /// </summary>
    internal void Discard(int frames)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
 
        if (frames <= 0)
        {
            return;
        }
 
        if (frames >= FrameCount)
        {
            FrameCount = 0;
            return;
        }
 
        int remaining = FrameCount - frames;
 
        _buffer.AsSpan(frames * Channels, remaining * Channels).CopyTo(_buffer);
        FrameCount = remaining;
    }
    
    internal ReadOnlySpan<float> AsSpan()
    {
        ThrowIfDisposed();
        return _buffer.AsSpan(0, FrameCount * Channels);
    }
    
    internal void EnsureCapacity(int requiredFrames)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(requiredFrames);
 
        if (requiredFrames * Channels <= _buffer.Length)
        {
            return;
        }
        
        int newLength = Math.Max(requiredFrames * Channels, _buffer.Length * 2);
 
        float[] replacement = ArrayPool<float>.Shared.Rent(newLength);
        _buffer.AsSpan(0, FrameCount * Channels).CopyTo(replacement);
 
        ArrayPool<float>.Shared.Return(_buffer);
        _buffer = replacement;
    }
    
    internal void Clear()
    {
        ThrowIfDisposed();
        FrameCount = 0;
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_isDisposed, this);
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
    }
}
