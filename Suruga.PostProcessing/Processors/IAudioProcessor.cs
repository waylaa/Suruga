using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Primitives;

namespace Suruga.PostProcessing.Processors;

internal interface IAudioProcessor : IDisposable
{
    AudioProcessorStatus SendFrame(AudioFrameBuffer? frame);
    
    AudioProcessorStatus ReceiveFrame(out AudioFrameBuffer? frame);
}
