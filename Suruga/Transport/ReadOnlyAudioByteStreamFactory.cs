using Microsoft.Extensions.Logging;
using Suruga.Resolvers.Primitives;
using Suruga.Resolvers.Sources;

namespace Suruga.Transport;

/// <summary>
/// Factory for creating platform‑specific <see cref="ReadOnlyAudioByteStream"/> instances.
/// </summary>
/// <param name="clientFactory">The HTTP client factory used to obtain named clients.</param>
/// <param name="loggerFactory">The logging factory used to create stream‑specific loggers.</param>
internal sealed class ReadOnlyAudioByteStreamFactory(IHttpClientFactory clientFactory, ILoggerFactory loggerFactory)
{
    /// <summary>
    /// Creates a <see cref="ReadOnlyAudioByteStream"/> from the specified audio source.
    /// </summary>
    /// <param name="source">The audio source.</param>
    /// <returns>A configured audio byte stream ready for reading.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="source"/> is of an unsupported type.
    /// </exception>
    internal ReadOnlyAudioByteStream Create(StreamSource source)
    {
        return source switch
        {
            LocalStreamSource { FilePath: string filePath }
                => new Local.LocalReadOnlyAudioByteStream(filePath, loggerFactory.CreateLogger<Local.LocalReadOnlyAudioByteStream>()),
            
            YoutubeStreamSource { Formats: IReadOnlyList<AdaptiveFormat> formats } => new Youtube.YoutubeReadOnlyAudioByteStream
            (
                clientFactory.CreateClient("youtube-bytestream"),
                formats,
                loggerFactory.CreateLogger<Youtube.YoutubeReadOnlyAudioByteStream>()
            ),
            
            _ => throw new InvalidOperationException("Unsupported platform.")
        };
    }
}
