using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Suruga.Primitives;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Suruga.Audio.Decode;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Resolvers.Local;

// False warning caused by JsonNodeExtensions's new extension keyword.
#pragma warning disable CS8620

/// <summary>
/// Resolves local audio files into <see cref="Track"/> instances.
/// </summary>
/// <param name="logger"></param>
internal partial class LocalTrackResolver(ILogger<LocalTrackResolver> logger) : ITrackResolver
{
    public TrackPlatform Platform => TrackPlatform.Local;
    
    /// <summary>
    /// Resolves a local file path into a single <see cref="Track"/>.
    /// </summary>
    /// <param name="input">The file path to resolve.</param>
    /// <param name="requestedBy"></param>
    /// <param name="token">A token that can cancel the resolution operation.</param>
    /// <returns>
    /// A task that completes with a read-only list containing the
    /// resolved track, or an empty list if resolution fails.
    /// </returns>
    public unsafe ValueTask<Result<TrackSet>> ResolveAsync(Input input, TrackRequestContext requestedBy, CancellationToken token = default)
    {
        string filePath = input.Value;
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            using FormatContext formatContext = new(filePath);
            formatContext.GetStream(out AVCodec* _, out AVStream* pStream);

            AVDictionary* metadata = formatContext.Pointer->metadata;
            AVDictionary* streamMetadata = pStream->metadata;

            if (!TryGetMetadata(metadata, "title", out string? title))
            {
                title = fileName;
            }

            if (!TryGetMetadata(metadata, "artist", out string? artist))
            {
                artist = TryGetMetadata(pStream->metadata, "album_artist", out string? albumArtist) ? albumArtist : "Unknown";
            }

            long durationInTimeBaseUnits = formatContext.Pointer->duration;

            if (durationInTimeBaseUnits <= 0)
                if (TryGetMetadata(streamMetadata, "DURATION", out string? dur))
                    if (long.TryParse(dur, out durationInTimeBaseUnits))
                    {
                    }

            TimeSpan? duration = TimeSpan.FromSeconds(durationInTimeBaseUnits / (double)AV_TIME_BASE);

            Track track = new()
            {
                Platform = TrackPlatform.Local,
                Id = fileName,
                Uri = filePath,
                Title = title,
                Author = artist,
                Duration = duration,
                RequestedBy = requestedBy
            };

            return ValueTask.FromResult(Result<TrackSet>.Success(new TrackSet([track])));
        }
        catch (Exception ex)
        {
            LogError(ex, ex.Message);
            return ValueTask.FromResult(Result<TrackSet>.Failure(ex));
        }
    }

    /// <summary>
    /// Attempts to retrieve a metadata value from an FFmpeg dictionary.
    /// </summary>
    /// <param name="dictionary">The FFmpeg metadata dictionary to search.</param>
    /// <param name="key">The key of the metadata entry to retrieve.</param>
    /// <param name="value">When this method returns, contains the metadata value if found.</param>
    /// <returns>
    /// <see langword="true"/> if the metadata was found. Otherwise, <see langword="false"/>.
    /// </returns>
    private static unsafe bool TryGetMetadata(AVDictionary* dictionary, string key, [NotNullWhen(true)] out string? value)
    {
        value = null;
        
        if (dictionary is null)
        {
            return false;
        }
        
        AVDictionaryEntry* entry = av_dict_get(dictionary, key, null, AV_DICT_IGNORE_SUFFIX);

        if (entry is null || entry->value is null)
        {
            return false;
        }

        ReadOnlySpan<byte> utf8Value = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(entry->value);
        value = Encoding.UTF8.GetString(utf8Value);
        
        return true;
    }
    
    /// <summary>
    /// Logs an error that occurred during track resolution.
    /// </summary>
    [LoggerMessage(LogLevel.Error, Message = "{Message}")]
    private partial void LogError(Exception exception, string message);
}
