using System.Buffers;

namespace Suruga.Audio;

internal sealed class DecodedFrame<T> : IDisposable where T : unmanaged
{
    internal int SamplePerChannelCount => SampleCount / 2;

    internal int SampleCount { get; set; }

    private readonly IMemoryOwner<T> _owner;
    private int _length;

    private bool _isDisposed;

    internal DecodedFrame(int length)
    {
        _owner = MemoryPool<T>.Shared.Rent(length);
        _length = length;

        SampleCount = length;
    }

    /// <summary>
    /// Create a stack-only pinned view of the memory for native interop.
    /// </summary>
    internal PinnedDecodedFrame Pin()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return new PinnedDecodedFrame(_owner.Memory);
    }

    internal ReadOnlyDecodedFrameView GetReadOnlyView()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return new ReadOnlyDecodedFrameView(_owner.Memory, offset: 0, _length);
    }

    internal ReadOnlyDecodedFrameView Slice(int offset, int length)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return new ReadOnlyDecodedFrameView(_owner.Memory, offset, length);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _owner.Dispose();
        _length = 0;
    }

    internal ref struct PinnedDecodedFrame : IDisposable
    {
        private MemoryHandle _handle;

        private int _length;
        private bool _isDisposed;

        internal PinnedDecodedFrame(Memory<T> buffer)
        {
            _handle = buffer.Pin();
            _length = buffer.Length;
        }

        internal readonly unsafe TResult WithPinnedSpan<TResult, TState>(TState state, Func<Span<T>, TState, TResult> action) where TState : allows ref struct
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(PinnedDecodedFrame));
            return action(new Span<T>((T*)_handle.Pointer, _length), state);
        }

        internal readonly unsafe void WithPinnedSpan(Action<Span<T>> action)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(PinnedDecodedFrame));
            action(new Span<T>((T*)_handle.Pointer, _length));
        }

        internal readonly unsafe void WithPinnedSpan<TState>(TState state, SpanAction<T, TState> action) where TState : allows ref struct
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(PinnedDecodedFrame));
            action(new Span<T>((T*)_handle.Pointer, _length), state);
        }

        internal readonly unsafe void WithPinnedHandle<TState>(TState state, Action<nint, int, TState> action)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(PinnedDecodedFrame));
            action((nint)_handle.Pointer, _length, state);
        }

        internal readonly unsafe TResult WithPinnedHandle<TResult, TState>(TState state, Func<nint, int, TState, TResult> action)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(PinnedDecodedFrame));
            return action((nint)_handle.Pointer, _length, state);
        }

        void IDisposable.Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _handle.Dispose();
            _length = 0;
        }
    }

    internal readonly ref struct ReadOnlyDecodedFrameView
    {
        internal ReadOnlySpan<T> Buffer => _buffer;

        internal int Length => _buffer.Length;

        internal ref readonly T this[int index]
            => ref _buffer[index];

        private readonly ReadOnlySpan<T> _buffer;

        internal ReadOnlyDecodedFrameView(ReadOnlyMemory<T> memory, int offset, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, memory.Length);

            _buffer = memory.Span.Slice(offset, length);
        }
    }
}
