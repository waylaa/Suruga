using Suruga.Primitives;
using Suruga.Transport.Abstractions;

namespace Suruga.Transport.Factories;

internal sealed class CompositeAudioByteStreamFactory
{
    private readonly IEnumerable<IAudioByteStreamFactory> _byteStreamFactories;
    
    internal CompositeAudioByteStreamFactory(IEnumerable<IAudioByteStreamFactory> byteStreamFactories)
        => _byteStreamFactories = byteStreamFactories;
    
    internal Result<IAudioByteStream> Create(AudioTrack track)
    {
        foreach (IAudioByteStreamFactory factory in _byteStreamFactories)
        {
            if (factory.CanHandle(track.Platform))
            {
                return Result<IAudioByteStream>.Success(factory.Create(track));
            }
        }

        return Result<IAudioByteStream>.Failure($"No byte stream could be created for '{track.Title}'.");
    }
}
