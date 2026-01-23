using Suruga.Primitives;
using Suruga.Transport.Abstractions;

namespace Suruga.Transport.Factories;

internal sealed class LocalAudioByteStreamFactory : IAudioByteStreamFactory
{
    public bool CanHandle(AudioPlatform platform)
        => platform is AudioPlatform.Local;

    public IAudioByteStream Create(AudioTrack track)
        => new LocalAudioByteStream(track);
}