using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Primitives;

public sealed class AudioFramebuffer
{
    public Memory<byte> Buffer => _buffer.AsMemory(0, LengthInBytes);

    public Span<float> Samples => MemoryMarshal.Cast<byte, float>(_buffer.AsSpan(0, LengthInBytes));

    public bool IsEmpty => FrameCount == 0;
    
    private int LengthInBytes => checked(FrameCount * Channels * sizeof(float));
    
    private int FrameCount { get; set; }

    private int Channels { get; }

    private byte[] _buffer;

    public AudioFramebuffer(int frameCount, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        
        FrameCount = frameCount;
        Channels = channels;
        
        _buffer = GC.AllocateUninitializedArray<byte>(LengthInBytes);
    }

    public void Resize(int frameCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);
        int requiredFramesInBytes = checked(frameCount * Channels * sizeof(float));

        if (requiredFramesInBytes > _buffer.Length)
        {
            byte[] newBuffer = GC.AllocateUninitializedArray<byte>(requiredFramesInBytes);
            _buffer.AsSpan(0, LengthInBytes).CopyTo(newBuffer);
            
            _buffer = newBuffer;
        }

        FrameCount = frameCount;
    }
}
