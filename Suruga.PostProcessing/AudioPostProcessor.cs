using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Extensions;
using Suruga.PostProcessing.Primitives;

namespace Suruga.PostProcessing;

public sealed class AudioPostProcessor : IDisposable
{
    private readonly Resampler _resampler = new();
    private readonly TimeStretch _timeStretch = new();
    
    private float[] _stageA = ArrayPool<float>.Shared.Rent(16384);
    private float[] _stageB = ArrayPool<float>.Shared.Rent(16384);

    private double _gain = 1;
    private double _tempo = 1;
    private double _pitch = 1;
    private double _rate = 1;

    private FlushStage _flushStage = FlushStage.Resampler;
    private bool _isFlushed;

    public void SetGain(double gain)
        => _gain = gain;

    public void SetTempo(double tempo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tempo, 0);

        _tempo = tempo;
        _rate = 1;
        
        Reconfigure();
    }

    public void SetPitch(double pitch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pitch, 0);

        _pitch = pitch;
        _rate = 1;
        
        Reconfigure();
    }

    public void SetRate(double rate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rate, 0);

        _rate = rate;
        _tempo = 1;
        _pitch = 1;
        
        Reconfigure();
    }

    public bool TryPostProcessFrame(AudioChunk input, [NotNullWhen(true)] out AudioChunk? output)
    {
        Span<float> samples = input.Samples;

        if (!_gain.IsApproximatelyEqualTo(1))
        {
            GainProcessor.Process(samples, _gain);
        }
        
        int frames = samples.Length / 2;
        EnsureStageCapacity(samples.Length);
        
        samples.CopyTo(_stageA);
        int stageFrames = frames;

        if (!(_rate * _pitch).IsApproximatelyEqualTo(1))
        {
            _resampler.Ratio = _rate * _pitch;

            stageFrames = _resampler.Process(_stageA.AsSpan(0, samples.Length), _stageB);
            (_stageA, _stageB) = (_stageB, _stageA);
        }

        double tempo = _tempo / _pitch;

        if (!tempo.IsApproximatelyEqualTo(1))
        {
            _timeStretch.Tempo = tempo;
            
            _timeStretch.Put(_stageA.AsSpan(0, stageFrames * 2));
            int produced = _timeStretch.Process(_stageB);

            if (produced > 0)
            {
                output = new AudioChunk(produced * 2);
                _stageB.AsSpan(0, produced * 2).CopyTo(output.Samples);

                input.Dispose();
                return true;
            }
        }
        else if (stageFrames > 0)
        {
            output = new AudioChunk(stageFrames * 2);
            _stageA.AsSpan(0, stageFrames * 2).CopyTo(output.Samples);
            
            input.Dispose();
            return true;
        }

        input.Dispose();
        output = null;
        
        return false;
    }

    public bool TryFlush([NotNullWhen(true)] out AudioChunk? output)
    {
        while (true)
        {
            switch (_flushStage)
            {
                case FlushStage.Resampler:
                {
                    if (_isFlushed)
                    {
                        output = null;
                        return false;
                    }

                    int resampledFrames = _resampler.Flush(_stageA);

                    if (resampledFrames > 0)
                    {
                        _timeStretch.Put(_stageA.AsSpan(0, resampledFrames * 2));
                        continue;
                    }
                    
                    EnsureStageCapacity(_timeStretch.AvailableFrames * 2);
                    _flushStage = FlushStage.TimeStretchHops;
                    
                    continue;
                }

                case FlushStage.TimeStretchHops:
                {
                    int produced = _timeStretch.Process(_stageB);

                    if (produced > 0)
                    {
                        output = new AudioChunk(produced * 2);
                        _stageB.AsSpan(0, produced * 2).CopyTo(output.Samples);
                        
                        return true;
                    }

                    _flushStage = FlushStage.TimeStretchTail;
                    continue;
                }

                case FlushStage.TimeStretchTail:
                {
                    int produced = _timeStretch.Flush(_stageB);

                    if (produced > 0)
                    {
                        output = new AudioChunk(produced * 2);
                        _stageB.AsSpan(0, produced * 2).CopyTo(output.Samples);
                        
                        return true;
                    }

                    _flushStage = FlushStage.Done;
                    _isFlushed = true;
                    
                    continue;
                }

                case FlushStage.Done:
                default:
                {
                    output = null;
                    return false;
                }
            }
        }
    }

    public void Reset()
    {
        _resampler.Reset();
        _timeStretch.Reset();
        
        _isFlushed = false;
    }

    private void Reconfigure()
    {
        _timeStretch.WindowScale = 1.0 / (_rate * _pitch); 
        
        _resampler.Reset();
        _timeStretch.Reset();

        _isFlushed = false;
    }

    private void EnsureStageCapacity(int required)
    {
        if (required <= _stageA.Length && required <= _stageB.Length)
        {
            return;
        }

        int size = Math.Max(required, Math.Max(_stageA.Length, _stageB.Length) * 2);
        float[] a = ArrayPool<float>.Shared.Rent(size);
        float[] b = ArrayPool<float>.Shared.Rent(size);

        ArrayPool<float>.Shared.Return(_stageA);
        ArrayPool<float>.Shared.Return(_stageB);

        _stageA = a;
        _stageB = b;
    }

    public void Dispose()
    {
        _resampler.Dispose();
        _timeStretch.Dispose();

        ArrayPool<float>.Shared.Return(_stageA);
        ArrayPool<float>.Shared.Return(_stageB);
    }
}
