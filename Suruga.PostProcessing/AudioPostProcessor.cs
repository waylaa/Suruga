using System.Diagnostics.CodeAnalysis;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Primitives;
using Suruga.PostProcessing.Processors;

namespace Suruga.PostProcessing;

public sealed class AudioPostProcessor
{
    private readonly TimeStretch _timeStretch = new(Channels);
    private readonly Resampler _resampler = new(Channels);
    private readonly GainProcessor _gain = new();

    private float _logicalTempo = 1;
    private float _pitch = 1;
    private float _logicalRate = 1;
    
    private const int Channels = 2;

    public bool TryPostProcessFrame(AudioFramebuffer input, [NotNullWhen(true)] out AudioFramebuffer? output)
    {
        output = null;

        if (input.IsEmpty)
        {
            output = input;
            return true;
        }
        
        if (!TryProcessStage(_timeStretch, input, out AudioFramebuffer? wsolaFrame))
        {
            return false;
        }
        
        if (!TryProcessStage(_resampler, wsolaFrame, out AudioFramebuffer? resamplerFrame))
        {
            return false;
        }

        if (!TryProcessStage(_gain, resamplerFrame, out AudioFramebuffer? finalFrame))
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
        AudioFramebuffer input,
        [NotNullWhen(true)] out AudioFramebuffer? output
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
}
