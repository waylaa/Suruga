using Suruga.Primitives;

namespace Suruga.Transport.Abstractions;

internal interface IAudioByteStreamFactory
{
    bool CanHandle(AudioPlatform platform);
    
    IAudioByteStream Create(AudioTrack track);
}