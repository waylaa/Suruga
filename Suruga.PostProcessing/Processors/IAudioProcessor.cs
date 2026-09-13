using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Primitives;

namespace Suruga.PostProcessing.Processors;

internal interface IAudioProcessor
{
    AudioProcessorStatus SendFrame(AudioFramebuffer? frame);
    
    AudioProcessorStatus ReceiveFrame(out AudioFramebuffer? frame);
}
