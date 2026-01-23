using Suruga.Primitives;
using Suruga.Transport.Abstractions;

namespace Suruga.Transport.Factories;

internal sealed class YoutubeAudioByteStreamFactory : IAudioByteStreamFactory
{
    public bool CanHandle(AudioPlatform platform)
        => platform is AudioPlatform.Youtube;

    public IAudioByteStream Create(AudioTrack track)
        => new YoutubeAudioByteStream(track);
}