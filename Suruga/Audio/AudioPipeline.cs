using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing;

namespace Suruga.Audio;

internal sealed class AudioPipeline
{
    private readonly AudioDecoder _decoder;
    private readonly AudioPostProcessor _postProcessor;

    internal AudioPipeline(AudioDecoder decoder, AudioPostProcessor postProcessor)
    {
        _decoder = decoder;
        _postProcessor = postProcessor;
    }
    
    internal IEnumerable<AudioChunk> GetAudioChunks(CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_decoder.TryDecodeNextChunk(out AudioChunk? decoded, token))
            {
                break;
            }
            
            if (!_postProcessor.TryPostProcessFrame(decoded, out AudioChunk? postProcessed))
            {
                continue;
            }
            
            yield return postProcessed;
        }

        while (!token.IsCancellationRequested && _postProcessor.TryFlush(out AudioChunk? flushed))
        {
            yield return flushed;
        }
    }
}
