using System.Buffers;
using System.Runtime.InteropServices;

namespace Suruga.Audio.Decode.Primitives;

/// <summary>
/// Represents a resizable audio frame buffer backed by managed memory.
/// </summary>
internal sealed class ManagedAudioFrameBuffer : IAudioFrameBuffer
{
	public ReadOnlyMemory<byte> Buffer => _owner.Memory[..SampleCountInBytes];
	
	public Span<float> Samples => MemoryMarshal.Cast<byte, float>(_owner.Memory.Span[..SampleCountInBytes]);

	public int SampleCount { get; private set; }
	
	/// <summary>
	/// Gets the number of samples stored in the current buffer, represented as bytes.
	/// </summary>
	private int SampleCountInBytes => SampleCount * sizeof(float);

	private readonly IMemoryOwner<byte> _owner;
	
	private bool _isDisposed;

    /// <summary>
    /// Initializes a new managed audio frame buffer.
    /// </summary>
    /// <param name="sampleCount">The initial number of samples to allocate.</param>
    internal ManagedAudioFrameBuffer(int sampleCount)
	{
		SampleCount = sampleCount;
		_owner = MemoryPool<byte>.Shared.Rent(SampleCountInBytes);
	}

    /// <summary>
    /// Resizes the buffer to accommodate the specified number of samples.
    /// </summary>
    /// <param name="newSampleCount">The new sample count.</param>
    public void Resize(int newSampleCount)
	{
		if (newSampleCount == SampleCount)
		{
			return;
		}
		
		SampleCount = newSampleCount;
	}

    /// <summary>
    /// Releases the rented memory backing the buffer.
    /// </summary>
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
