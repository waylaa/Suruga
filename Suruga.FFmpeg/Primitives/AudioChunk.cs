using System.Buffers;
using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Primitives;

/// <summary>
/// Represents a resizable decoded audio chunk backed by managed pooled memory.
/// </summary>
public sealed class AudioChunk : IDisposable
{
    public ReadOnlyMemory<byte> Buffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return _owner.Memory[..SampleCountInBytes];
        }
    }

    public Span<float> Samples
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return MemoryMarshal.Cast<byte, float>(_owner.Memory.Span[..SampleCountInBytes]);
        }
    }
    
    public int SampleCount { get; private set; }

    private int SampleCountInBytes => SampleCount * sizeof(float);

    private IMemoryOwner<byte> _owner;
    private bool _isDisposed;

    public AudioChunk(int sampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);

        SampleCount = sampleCount;
        _owner = MemoryPool<byte>.Shared.Rent(SampleCountInBytes);
    }

    internal void Resize(int sampleCount)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);

        if (sampleCount == SampleCount)
        {
            return;
        }
        
        int requiredBytes = sampleCount * sizeof(float);

        if (requiredBytes > SampleCountInBytes)
        {
            IMemoryOwner<byte> newOwner = MemoryPool<byte>.Shared.Rent(requiredBytes);
            _owner.Memory.Span[..SampleCountInBytes].CopyTo(newOwner.Memory.Span);
            
            _owner.Dispose();
            _owner = newOwner;
        }
        
        SampleCount = sampleCount;
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        _owner.Dispose();
        SampleCount = 0;
    }
}
