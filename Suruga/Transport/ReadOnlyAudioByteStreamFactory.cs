using Suruga.Resolvers.Primitives;
using Suruga.Resolvers.Sources;
using Suruga.Transport.Local;
using Suruga.Transport.Youtube;

namespace Suruga.Transport;

internal sealed class ReadOnlyAudioByteStreamFactory(IHttpClientFactory clientFactory)
{
    internal ReadOnlyAudioByteStream Create(StreamSource source) => source switch
    {
        LocalStreamSource { FilePath: string filePath } => new LocalReadOnlyAudioByteStream(filePath),
        YoutubeStreamSource { Formats: IReadOnlyList<AdaptiveFormat> formats } => new YoutubeReadOnlyAudioByteStream
        (
            clientFactory.CreateClient("youtube-bytestream"),
            formats
        ),
        _ => throw new InvalidOperationException("Unsupported platform.")
    };
}
