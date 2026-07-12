using System.Buffers;
using System.Runtime.InteropServices;

namespace Suruga.Audio.Decode.Primitives;

/// <summary>
/// Represents an audio frame buffer backed by an unmanaged <see cref="FFmpeg.AutoGen.AVFrame"/>.
/// </summary>
internal sealed unsafe class UnmanagedAudioFrameBuffer : IAudioFrameBuffer
{
	public ReadOnlyMemory<byte> Buffer => _bufferManager.Memory;
	
	public Span<float> Samples => MemoryMarshal.Cast<byte, float>(_bufferManager.Memory.Span);

	public int SampleCount { get; }

	private readonly Frame _nativeFrame;
	private readonly NativeMemoryManager<byte> _bufferManager;

    /// <summary>
    /// Initializes a new unmanaged audio frame buffer.
    /// </summary>
    /// <param name="nativeFrame">The native frame that owns the audio data.</param>
    internal UnmanagedAudioFrameBuffer(Frame nativeFrame)
	{
		_nativeFrame = nativeFrame;
		
		SampleCount = _nativeFrame.SampleCountPerChannel * _nativeFrame.Pointer->ch_layout.nb_channels;
		_bufferManager = new NativeMemoryManager<byte>(_nativeFrame.ExtendedData[0], SampleCount * sizeof(float));
	}

    /// <summary>
    /// Releases the underlying native frame reference.
    /// </summary>
    public void Dispose()
		=> _nativeFrame.Unreference();

    /// <summary>
    /// Exposes unmanaged memory through the <see cref="Memory{T}"/> API.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NativeMemoryManager<T> : MemoryManager<T> where T : unmanaged
	{
		private readonly T* _pointer;
		private readonly int _length;

        /// <summary>
        /// Initializes a new memory manager for the specified unmanaged buffer.
        /// </summary>
        /// <param name="pointer">A pointer to the buffer.</param>
        /// <param name="length">The buffer length in elements.</param>
        internal NativeMemoryManager(T* pointer, int length)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
			
			_pointer = pointer;
			_length = length;
		}

		public override Span<T> GetSpan()
			=> new(_pointer, _length);

		public override MemoryHandle Pin(int elementIndex = 0)
		{
			if (elementIndex < 0 || elementIndex >= _length)
			{
				throw new ArgumentOutOfRangeException(nameof(elementIndex));
			}

			return new MemoryHandle(_pointer + elementIndex);
		}

		public override void Unpin()
		{
		}

		protected override void Dispose(bool disposing)
		{
		}
	}
}
