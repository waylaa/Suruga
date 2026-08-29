using System.Buffers;
using System.Numerics;

namespace Suruga.PostProcessing;

internal sealed class TimeStretch : IDisposable
{
    internal int AvailableFrames => _inputFrames;

    /// <summary>
    /// How much larger the analysis/overlap/search windows need to be,
    /// relative to their base sizes.
    /// </summary>
    internal double WindowScale
    {
        get;
        set
        {
            double clamped = Math.Clamp(value, 1.0, MaxWindowScale);

            if (Math.Abs(clamped - field) < 1e-6)
            {
                return;
            }

            field = clamped;
            RecomputeWindowSizes();
        }
    } = 1;
    
    internal double Tempo { get; set; } = 1.0;
    
    private readonly int _overlapCapacityFrames;
    private readonly float[] _overlap;
    private readonly float[] _window;

    private float[] _input;
    
    private int _sequenceLength;
    private int _overlapLength;
    private int _hopLength;
    private int _searchRadius;

    private int _inputFrames;
    private bool _isFirst;
    
    private const int Channels = 2;
    
    private const int BaseSequenceLength = 2048;
    private const int BaseOverlapLength = 512;
    private const int BaseSearchRadius = 512;
    
    private const double MaxWindowScale = 4.0;

    internal TimeStretch(int capacityFrames = 16384)
    {
        _overlapCapacityFrames = (int)Math.Ceiling(BaseOverlapLength * MaxWindowScale);
        _overlap = ArrayPool<float>.Shared.Rent(_overlapCapacityFrames * Channels);
        _window = ArrayPool<float>.Shared.Rent(_overlapCapacityFrames);
        _input = ArrayPool<float>.Shared.Rent(capacityFrames * Channels);

        RecomputeWindowSizes();
    }

    internal void Reset()
    {
        _inputFrames = 0;
        _isFirst = true;

        _overlap.AsSpan(0, _overlapLength * Channels).Clear();
    }

    internal void Put(ReadOnlySpan<float> samples)
    {
        int frames = samples.Length / Channels;

        EnsureCapacity(_inputFrames + frames);
        samples.CopyTo(_input.AsSpan(_inputFrames * Channels));

        _inputFrames += frames;
    }

    internal int Process(Span<float> destination)
    {
        int outputFrames = 0;
        double analysisHop = _hopLength * Tempo;

        while (true)
        {
            int required = _sequenceLength + _searchRadius;

            if (_inputFrames < required)
            {
                break;
            }

            if (destination.Length < (outputFrames + _hopLength) * Channels)
            {
                break;
            }

            ReadOnlySpan<float> input = _input.AsSpan(0, _inputFrames * Channels);
            bool isFirstHop = _isFirst;
            int position;

            if (_isFirst)
            {
                position = 0;
                _isFirst = false;
            }
            else
            {
                position = FindBestPosition(input);
            }

            ReadOnlySpan<float> sequence = input.Slice(position * Channels, _sequenceLength * Channels);
            Span<float> output = destination.Slice(outputFrames * Channels, _hopLength * Channels);

            if (isFirstHop)
            {
                sequence[..(_hopLength * Channels)].CopyTo(output);
            }
            else
            {
                OverlapAdd(output, sequence);
            }

            sequence.Slice(_hopLength * Channels, _overlapLength * Channels).CopyTo(_overlap);
            
            outputFrames += _hopLength;
            int advance = Math.Max(1, (int)Math.Round(analysisHop));

            RemoveInput(advance);
        }

        return outputFrames;
    }
    
    internal int Flush(Span<float> destination)
    {
        if (_inputFrames == 0)
        {
            return 0;
        }

        int destinationFrames = destination.Length / Channels;
        int totalFrames = Math.Min(_inputFrames, destinationFrames);

        ReadOnlySpan<float> input = _input.AsSpan(0, totalFrames * Channels);

        if (_isFirst)
        {
            // No overlap context has been established yet - nothing to
            // crossfade against, so just emit the remainder as-is.
            input.CopyTo(destination);
        }
        else
        {
            int overlapFrames = Math.Min(_overlapLength, totalFrames);

            for (int frame = 0; frame < overlapFrames; frame++)
            {
                float fadeIn = _window[frame];
                float fadeOut = 1.0f - fadeIn;

                int i = frame * Channels;

                destination[i] = _overlap[i] * fadeOut + input[i] * fadeIn;
                destination[i + 1] = _overlap[i + 1] * fadeOut + input[i + 1] * fadeIn;
            }

            int remainderFrames = totalFrames - overlapFrames;

            if (remainderFrames > 0)
            {
                input.Slice(overlapFrames * Channels, remainderFrames * Channels)
                    .CopyTo(destination[(overlapFrames * Channels)..]);
            }
        }

        RemoveInput(totalFrames);
        return totalFrames;
    }

    private int FindBestPosition(ReadOnlySpan<float> input)
    {
        ReadOnlySpan<float> reference = _overlap.AsSpan(0, _overlapLength * Channels);
        
        const int begin = 0;
        int end = Math.Min(_inputFrames - _sequenceLength, _searchRadius);
        float best = float.NegativeInfinity;
        int bestPosition = 0;

        for (int position = begin; position <= end; position++)
        {
            ReadOnlySpan<float> candidate = input.Slice(position * Channels, _overlapLength * Channels);
            float similarity = NormalizedCosineSimilarity(reference, candidate);
            float distance = MathF.Abs(position) / _searchRadius;
            
            similarity -= distance * 0.015f;

            if (similarity > best)
            {
                best = similarity;
                bestPosition = position;
            }
        }

        return bestPosition;
    }
    
    private static float NormalizedCosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("The spans must have the same length.", nameof(b));
        }

        if (a.Length == 0)
        {
            return 0;
        }

        int width = Vector<float>.Count;
        int vectorEnd = a.Length - a.Length % width;

        Vector<float> dotAccumulator = Vector<float>.Zero;
        Vector<float> energyAAccumulator = Vector<float>.Zero;
        Vector<float> energyBAccumulator = Vector<float>.Zero;

        int i = 0;

        for (; i < vectorEnd; i += width)
        {
            Vector<float> va = new(a.Slice(i, width));
            Vector<float> vb = new(b.Slice(i, width));

            dotAccumulator += va * vb;
            energyAAccumulator += va * va;
            energyBAccumulator += vb * vb;
        }

        double dot = Vector.Sum(dotAccumulator);
        double energyA = Vector.Sum(energyAAccumulator);
        double energyB = Vector.Sum(energyBAccumulator);

        for (; i < a.Length; i++)
        {
            float va = a[i];
            float vb = b[i];

            dot += va * vb;
            energyA += va * va;
            energyB += vb * vb;
        }

        double denominator = Math.Sqrt(energyA * energyB);

        if (denominator < 1e-9)
        {
            return 0;
        }

        return (float)(dot / denominator);
    }

    private void OverlapAdd(Span<float> destination, ReadOnlySpan<float> input)
    {
        ReadOnlySpan<float> current = input[..(_overlapLength * Channels)];

        for (int frame = 0; frame < _overlapLength; frame++)
        {
            float fadeIn = _window[frame];
            float fadeOut = 1.0f - fadeIn;
            
            int i = frame * Channels;

            destination[i] = _overlap[i] * fadeOut + current[i] * fadeIn;
            destination[i + 1] = _overlap[i + 1] * fadeOut + current[i + 1] * fadeIn;
        }

        input.Slice(_overlapLength * Channels, (_hopLength - _overlapLength) * Channels)
            .CopyTo(destination[(_overlapLength * Channels)..]);
    }

    private void BuildWindow()
    {
        for (int i = 0; i < _overlapLength; i++)
        {
            // Equal-power crossfade.
            double x = (i + 0.5) / _overlapLength;
            _window[i] = (float)Math.Sin(x * Math.PI * 0.5);
        }
    }

    private void RemoveInput(int frames)
    {
        if (frames <= 0)
        {
            return;
        }

        if (frames >= _inputFrames)
        {
            _inputFrames = 0;
            return;
        }

        int remaining = _inputFrames - frames;

        _input.AsSpan(frames * Channels, remaining * Channels).CopyTo(_input);
        _inputFrames = remaining;
    }

    private void EnsureCapacity(int frames)
    {
        if (frames * Channels <= _input.Length)
        {
            return;
        }

        int newLength = Math.Max(frames * Channels, _input.Length * 2);

        float[] replacement = ArrayPool<float>.Shared.Rent(newLength);
        _input.AsSpan(0, _inputFrames * Channels).CopyTo(replacement);

        ArrayPool<float>.Shared.Return(_input);
        _input = replacement;
    }

    private void RecomputeWindowSizes()
    {
        int overlapLength = Math.Min(_overlapCapacityFrames, Math.Max(2, (int)Math.Round(BaseOverlapLength * WindowScale)));
        int sequenceLength = Math.Max(overlapLength + 2, (int)Math.Round(BaseSequenceLength * WindowScale));
        int searchRadius = Math.Max(1, (int)Math.Round(BaseSearchRadius * WindowScale));

        _overlapLength = overlapLength;
        _sequenceLength = sequenceLength;
        _hopLength = sequenceLength - overlapLength;
        _searchRadius = searchRadius;

        BuildWindow();
        
        _overlap.AsSpan(0, _overlapLength * Channels).Clear();
        _isFirst = true;
    }

    public void Dispose()
    {
        ArrayPool<float>.Shared.Return(_overlap);
        ArrayPool<float>.Shared.Return(_window);
        ArrayPool<float>.Shared.Return(_input);
    }
}
