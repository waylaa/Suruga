using System.Numerics.Tensors;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Extensions;
using Suruga.PostProcessing.Primitives;

namespace Suruga.PostProcessing.Processors;

internal sealed class GainProcessor : IAudioProcessor
{
    internal float Gain
    {
        private get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;
    
    private AudioFrameBuffer? _pendingFrame;

    public AudioProcessorStatus SendFrame(AudioFrameBuffer? frame)
    {
        if (Gain.IsApproximatelyEqualTo(1))
        {
            _pendingFrame = frame;
            return AudioProcessorStatus.NoOp;
        }
        
        if (frame is null)
        {
            return AudioProcessorStatus.EndOfStream;
        }
        
        TensorPrimitives.Multiply(frame.Samples, Gain, frame.Samples);
        _pendingFrame = frame;
        
        return AudioProcessorStatus.Success;
    }

    public AudioProcessorStatus ReceiveFrame(out AudioFrameBuffer? frame)
    {
        if (_pendingFrame is not null)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
            
            return AudioProcessorStatus.Success;
        }

        frame = null;
        return AudioProcessorStatus.NeedMoreInput;
    }

    public void Reset()
        => _pendingFrame = null;

    public void Dispose()
    {
    }
}
