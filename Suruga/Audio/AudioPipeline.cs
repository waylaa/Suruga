using System.Runtime.CompilerServices;
using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing;

namespace Suruga.Audio;

internal sealed class AudioPipeline(AudioDecoder decoder, AudioPostProcessor postProcessor, AudioSink sink)
{
    internal async IAsyncEnumerable<AudioFramebuffer> GetAudioFrameBuffers([EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!decoder.TryDecodeNextFramebuffer(out AudioFramebuffer? decoded, token))
                {
                    break;
                }
            
                if (!postProcessor.TryPostProcessFrame(decoded, out AudioFramebuffer? postProcessed))
                {
                    continue;
                }
            
                yield return postProcessed;
            }
            
            if (!token.IsCancellationRequested)
            {
                foreach (AudioFramebuffer flushed in decoder.Flush())
                {
                    if (postProcessor.TryPostProcessFrame(flushed, out AudioFramebuffer? postProcessed))
                    {
                        yield return postProcessed;
                    }
                }
            }
        }
        finally
        {
            postProcessor.Reset();
            await sink.FlushAsync(CancellationToken.None);
        }
    }
}
