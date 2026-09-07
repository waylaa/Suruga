using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Extensions;
using Suruga.PostProcessing.Primitives;
using Suruga.PostProcessing.Resampling;
using Suruga.PostProcessing.Resampling.Buffers;

namespace Suruga.PostProcessing.Processors;

internal sealed class Resampler : IAudioProcessor
{
    internal float Rate
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;
    
    private readonly ILogger<Resampler> _logger;

    private readonly ResamplerBuffer _input;
    private readonly ResamplerState _state;
    private readonly int _channels;
    
    internal Resampler(int channels, ILogger<Resampler> logger)
    {
        _channels = channels;
        _logger = logger;
        
        _input = new ResamplerBuffer(1024, channels);
        _state = new ResamplerState(channels);
    }
    
    public AudioProcessorStatus SendFrame(AudioFrameBuffer? frame)
    {
        if (Rate.IsApproximatelyEqualTo(1))
        {
            return AudioProcessorStatus.NoOp;
        }

        if (frame is null)
        {
            return AudioProcessorStatus.EndOfStream;
        }

        if (!frame.IsEmpty)
        {
            _input.Write(frame.Samples);
        }
        
        return AudioProcessorStatus.Success;
    }

    public AudioProcessorStatus ReceiveFrame(out AudioFrameBuffer? frame)
    {
        frame = null;

        if (Rate.IsApproximatelyEqualTo(1))
        {
            return AudioProcessorStatus.NoOp;
        }

        const int outputFrames = 1920;

        if (!CanProduce(outputFrames))
        {
            return AudioProcessorStatus.NeedMoreInput;
        }
        
        frame = Resample(outputFrames);
        return AudioProcessorStatus.Success;
    }

    private bool CanProduce(int outputFrames)
    {
        int requiredInputFrames = (int)Math.Ceiling(_state.Fraction + outputFrames * Rate) + 2;
        return _input.AvailableFrames >= requiredInputFrames;
    }

    private AudioFrameBuffer Resample(int outputFrames)
    {
        AudioFrameBuffer output = new(outputFrames, _channels);

        Span<float> destination = output.Samples;
        int requiredInputFrames = (int)Math.Ceiling(_state.Fraction + outputFrames * Rate) + 2;
        ReadOnlySpan<float> input = _input.Peek(0, requiredInputFrames);

        int producedFrames = 0;
        int inputFrameIndex = 0;

        while (producedFrames < outputFrames && inputFrameIndex + 2 < requiredInputFrames)
        {
            WriteOutputFrame
            (
                input,
                destination,
                inputFrameIndex,
                producedFrames,
                _state.Fraction
            );
            
            producedFrames++;
            _state.Fraction += Rate;

            int framesToAdvance = (int)_state.Fraction;

            if (framesToAdvance > 0)
            {
                inputFrameIndex += framesToAdvance;
                _state.Fraction -= framesToAdvance;
            }
        }

        CommitConsumedInput(input, inputFrameIndex);
        _input.Advance(inputFrameIndex);
        
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteOutputFrame
    (
        ReadOnlySpan<float> input,
        Span<float> output,
        int inputFrameIndex,
        int outputFrameIndex,
        double fraction
    )
    {
        int destinationOffset = outputFrameIndex * _channels;

        for (int channel = 0; channel < _channels; channel++)
        {
            float p0;

            if (inputFrameIndex == 0)
            {
                p0 = _state.HasHistory ? _state.PreviousFrame[channel] : input[channel];
            }
            else
            {
                p0 = input[(inputFrameIndex - 1) * _channels + channel];
            }
            
            float p1 = input[inputFrameIndex * _channels + channel];
            float p2 = input[(inputFrameIndex + 1) * _channels + channel];
            float p3 = input[(inputFrameIndex + 2) * _channels + channel];

            output[destinationOffset + channel] = CatmullRomInterpolator.Interpolate(p0, p1, p2, p3, fraction);
        }
    }

    private void CommitConsumedInput(ReadOnlySpan<float> input, int consumedFrames)
    {
        if (consumedFrames <= 0)
        {
            return;
        }

        int lastFrameIndex = consumedFrames - 1;

        for (int channel = 0; channel < _channels; channel++)
        {
            _state.PreviousFrame[channel] = input[lastFrameIndex * _channels + channel];
        }

        _state.HasHistory = true;
    }
    
    public void Dispose()
    {
        _input.Clear();
        _state.Reset();
    }
}
