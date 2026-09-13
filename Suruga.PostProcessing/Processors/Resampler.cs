using System.Runtime.CompilerServices;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Extensions;
using Suruga.PostProcessing.Primitives;
using Suruga.PostProcessing.Resampling;
using Suruga.PostProcessing.Resampling.Buffers;

namespace Suruga.PostProcessing.Processors;

internal sealed class Resampler(int channels) : IAudioProcessor
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
    
    private readonly ResamplerBuffer _input = new(1024, channels);
    private readonly ResamplerState _state = new(channels);
    
    public AudioProcessorStatus SendFrame(AudioFramebuffer? frame)
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

    public AudioProcessorStatus ReceiveFrame(out AudioFramebuffer? frame)
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

    public void Reset()
    {
        _input.Clear();
        _state.Reset();
    }

    private bool CanProduce(int outputFrames)
    {
        int requiredInputFrames = (int)Math.Ceiling(_state.Fraction + outputFrames * Rate) + 2;
        return _input.AvailableFrames >= requiredInputFrames;
    }

    private AudioFramebuffer Resample(int outputFrames)
    {
        AudioFramebuffer output = new(outputFrames, channels);
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
        int destinationOffset = outputFrameIndex * channels;

        for (int channel = 0; channel < channels; channel++)
        {
            float p0;

            if (inputFrameIndex == 0)
            {
                p0 = _state.HasHistory ? _state.PreviousFrame[channel] : input[channel];
            }
            else
            {
                p0 = input[(inputFrameIndex - 1) * channels + channel];
            }
            
            float p1 = input[inputFrameIndex * channels + channel];
            float p2 = input[(inputFrameIndex + 1) * channels + channel];
            float p3 = input[(inputFrameIndex + 2) * channels + channel];

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

        for (int channel = 0; channel < channels; channel++)
        {
            _state.PreviousFrame[channel] = input[lastFrameIndex * channels + channel];
        }

        _state.HasHistory = true;
    }
}
