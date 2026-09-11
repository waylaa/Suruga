using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Primitives;
using Suruga.PostProcessing.Processors;

namespace Suruga.PostProcessing;

public sealed class AudioPostProcessor(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly TimeStretch _timeStretch = new(Channels, loggerFactory);
    private readonly Resampler _resampler = new(Channels, loggerFactory.CreateLogger<Resampler>());
    private readonly GainProcessor _gain = new();

    private float _logicalTempo = 1;
    private float _pitch = 1;
    private float _logicalRate = 1;
    
    private const int Channels = 2;

    public bool TryPostProcessFrame(AudioFrameBuffer input, [NotNullWhen(true)] out AudioFrameBuffer? output)
    {
        output = null;

        if (input.IsEmpty)
        {
            output = input;
            return true;
        }
        
        if (!TryProcessStage(_timeStretch, input, out AudioFrameBuffer? wsolaFrame))
        {
            return false;
        }
        
        if (!TryProcessStage(_resampler, wsolaFrame, out AudioFrameBuffer? resamplerFrame))
        {
            return false;
        }

        if (!TryProcessStage(_gain, resamplerFrame, out AudioFrameBuffer? finalFrame))
        {
            return false;
        }

        output = finalFrame;
        return true;
    }

    public void SetTempo(float value)
    {
        _logicalTempo = value;
        ApplyParameters();
    }

    public void SetPitch(float value)
    {
        _pitch = value;
        ApplyParameters();
    }

    public void SetRate(float value)
    {
        _logicalRate = value;
        ApplyParameters();
    }

    public void SetGain(float value)
        => _gain.Gain = value;

    public void Reset()
    {
        _timeStretch.Reset();
        _resampler.Reset();
        _gain.Reset();
    }

    private void ApplyParameters()
    {
        _timeStretch.Tempo = _logicalTempo / _pitch;
        _resampler.Rate = _pitch * _logicalRate;
    }

    private static bool TryProcessStage
    (
        IAudioProcessor processor,
        AudioFrameBuffer input,
        [NotNullWhen(true)] out AudioFrameBuffer? output
    )
    {
        AudioProcessorStatus sendStatus = processor.SendFrame(input);

        if (sendStatus is AudioProcessorStatus.NoOp)
        {
            output = input;
            return true;
        }

        if (sendStatus is not AudioProcessorStatus.Success)
        {
            output = null;
            return false;
        }

        AudioProcessorStatus receiveStatus = processor.ReceiveFrame(out output!);

        if (receiveStatus is AudioProcessorStatus.NoOp)
        {
            output = input;
            return true;
        }
        
        if (receiveStatus is not AudioProcessorStatus.Success)
        {
            output = null;
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _timeStretch.Dispose();
        _resampler.Dispose();
        _gain.Dispose();
    }
}
