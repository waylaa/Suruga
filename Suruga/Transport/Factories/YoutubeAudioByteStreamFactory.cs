using Suruga.Primitives;
using Suruga.Resolvers.Youtube;
using Suruga.Resolvers.Youtube.Clients;
using Suruga.Transport.Abstractions;
using Suruga.Transport.Primitives;

namespace Suruga.Transport.Factories;

internal sealed class YoutubeAudioByteStreamFactory : IAudioByteStreamFactory
{
    public bool CanHandle(AudioPlatform platform)
        => platform is AudioPlatform.Youtube;

    public IAudioByteStream Create(AudioTrack track)
        => new YoutubeAudioByteStream((YoutubeAudioStreamDescriptor)track.Stream);
}