using System.Buffers;
using System.Runtime.CompilerServices;

namespace Suruga.PostProcessing;

internal sealed class Resampler : IDisposable
{
    internal int PendingFrames => _pendingFrames;
    
    internal double Ratio
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;
    
    private const int Channels = 2;
    private const int InitialCapacityFrames = 2048;
    
    private double _fraction;

    private float _prevLeft;
    private float _prevRight;
    private bool _hasHistory;

    private float[] _pending = ArrayPool<float>.Shared.Rent(InitialCapacityFrames * Channels);
    private int _pendingFrames;

    private bool _isDisposed;
    
    internal int Process(ReadOnlySpan<float> source, Span<float> destination)
    {
        ThrowIfDisposed();

        if (source.Length % Channels != 0)
        {
            throw new ArgumentException("The source length must be divisible by the channel count.", nameof(source));
        }

        if (destination.Length % Channels != 0)
        {
            throw new ArgumentException("The destination length must be divisible by the channel count.", nameof(destination));
        }

        AppendPending(source);

        ReadOnlySpan<float> pending = _pending.AsSpan(0, _pendingFrames * Channels);
        int sourceFrames = _pendingFrames;
        int destinationFrames = destination.Length / Channels;

        int produced = 0;
        int index = 0;
        
        while (produced < destinationFrames && index + 2 < sourceFrames)
        {
            float p0Left, p0Right;

            if (index == 0)
            {
                if (_hasHistory)
                {
                    p0Left = _prevLeft;
                    p0Right = _prevRight;
                }
                else
                {
                    p0Left = pending[0];
                    p0Right = pending[1];
                }
            }
            else
            {
                int p0Index = (index - 1) * Channels;
                p0Left = pending[p0Index];
                p0Right = pending[p0Index + 1];
            }

            int p1Index = index * Channels;
            float p1Left = pending[p1Index];
            float p1Right = pending[p1Index + 1];

            int p2Index = (index + 1) * Channels;
            float p2Left = pending[p2Index];
            float p2Right = pending[p2Index + 1];

            int p3Index = (index + 2) * Channels;
            float p3Left = pending[p3Index];
            float p3Right = pending[p3Index + 1];

            double t = _fraction;

            int outIndex = produced * Channels;
            destination[outIndex] = CatmullRom(p0Left, p1Left, p2Left, p3Left, t);
            destination[outIndex + 1] = CatmullRom(p0Right, p1Right, p2Right, p3Right, t);

            produced++;
            _fraction += Ratio;

            int advance = (int)_fraction;

            if (advance > 0)
            {
                index += advance;
                _fraction -= advance;
            }
        }

        int framesFullyConsumed = Math.Min(index, sourceFrames);

        if (framesFullyConsumed > 0)
        {
            int lastIndex = (framesFullyConsumed - 1) * Channels;
            
            _prevLeft = pending[lastIndex];
            _prevRight = pending[lastIndex + 1];
            _hasHistory = true;

            RemovePendingFront(framesFullyConsumed);
        }

        return produced;
    }
    
    internal int Flush(Span<float> destination)
    {
        ThrowIfDisposed();

        if (_pendingFrames == 0)
        {
            return 0;
        }

        ReadOnlySpan<float> pending = _pending.AsSpan(0, _pendingFrames * Channels);
        int sourceFrames = _pendingFrames;

        int destinationFrames = destination.Length / Channels;
        int produced = 0;
        int index = 0;

        while (produced < destinationFrames && index < sourceFrames)
        {
            float p0Left, p0Right;

            if (index == 0)
            {
                (p0Left, p0Right) = _hasHistory ? (_prevLeft, _prevRight) : (pending[0], pending[1]);
            }
            else
            {
                int p0Index = (index - 1) * Channels;
                p0Left = pending[p0Index];
                p0Right = pending[p0Index + 1];
            }

            int p1Index = Math.Min(index, sourceFrames - 1) * Channels;
            float p1Left = pending[p1Index];
            float p1Right = pending[p1Index + 1];

            int p2Index = Math.Min(index + 1, sourceFrames - 1) * Channels;
            float p2Left = pending[p2Index];
            float p2Right = pending[p2Index + 1];

            int p3Index = Math.Min(index + 2, sourceFrames - 1) * Channels;
            float p3Left = pending[p3Index];
            float p3Right = pending[p3Index + 1];

            double t = _fraction;

            int outIndex = produced * Channels;
            destination[outIndex] = CatmullRom(p0Left, p1Left, p2Left, p3Left, t);
            destination[outIndex + 1] = CatmullRom(p0Right, p1Right, p2Right, p3Right, t);

            produced++;
            _fraction += Ratio;

            int advance = (int)_fraction;

            if (advance > 0)
            {
                index += advance;
                _fraction -= advance;
            }

            if (index >= sourceFrames)
            {
                break;
            }
        }

        int framesFullyConsumed = Math.Min(index, sourceFrames);

        if (framesFullyConsumed > 0)
        {
            int lastIndex = (framesFullyConsumed - 1) * Channels;
            _prevLeft = pending[lastIndex];
            _prevRight = pending[lastIndex + 1];
            _hasHistory = true;

            RemovePendingFront(framesFullyConsumed);
        }

        return produced;
    }

    internal void Reset()
    {
        _fraction = 0.0;
        _hasHistory = false;
        _prevLeft = 0f;
        _prevRight = 0f;
        _pendingFrames = 0;
    }

    private void AppendPending(ReadOnlySpan<float> source)
    {
        int incomingFrames = source.Length / Channels;

        if (incomingFrames == 0)
        {
            return;
        }

        EnsurePendingCapacity(_pendingFrames + incomingFrames);

        source.CopyTo(_pending.AsSpan(_pendingFrames * Channels));
        _pendingFrames += incomingFrames;
    }

    private void EnsurePendingCapacity(int requiredFrames)
    {
        if (requiredFrames * Channels <= _pending.Length)
        {
            return;
        }

        int newLength = Math.Max(requiredFrames * Channels, _pending.Length * 2);

        float[] replacement = ArrayPool<float>.Shared.Rent(newLength);
        _pending.AsSpan(0, _pendingFrames * Channels).CopyTo(replacement);

        ArrayPool<float>.Shared.Return(_pending);
        _pending = replacement;
    }

    private void RemovePendingFront(int frames)
    {
        if (frames <= 0)
        {
            return;
        }

        if (frames >= _pendingFrames)
        {
            _pendingFrames = 0;
            return;
        }

        int remaining = _pendingFrames - frames;

        _pending.AsSpan(frames * Channels, remaining * Channels).CopyTo(_pending);
        _pendingFrames = remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CatmullRom(float p0, float p1, float p2, float p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        return (float)(0.5 *
        (
            2.0 * p1 +
            (-p0 + p2) * t +
            (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
            (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)
        );
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
        ArrayPool<float>.Shared.Return(_pending);
    }
}
