using Suruga.Primitives;
using Microsoft.Extensions.Logging;
using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers.Local;

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
    public ValueTask<Result<TrackSet>> ResolveAsync(Input input, TrackRequestContext requestedBy, CancellationToken token = default)
    {
        string filePath = input.Value;
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            using FormatContext formatContext = new(filePath);
            StreamInfo info = formatContext.GetStream();

            DictionaryView formatContextMetadataView = new(in formatContext.Metadata);
            DictionaryView streamMetadataView = new(in info.Stream.Metadata);

            if (!formatContextMetadataView.TryGetValue("title", out string? title))
            {
                title = fileName;
            }

            if (!formatContextMetadataView.TryGetValue("artist", out string? artist))
            {
                artist = streamMetadataView.TryGetValue("album_artist", out string? albumArtist) ? albumArtist : "Unknown";
            }

            long durationInTimeBaseUnits = formatContext.Duration;

            if (durationInTimeBaseUnits <= 0)
                if (streamMetadataView.TryGetValue("DURATION", out string? dur))
                    if (long.TryParse(dur, out durationInTimeBaseUnits))
                    {
                    }

            TimeSpan? duration = TimeSpan.FromSeconds(durationInTimeBaseUnits / (double)Constants.AV_TIME_BASE);

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
    /// Logs an error that occurred during track resolution.
    /// </summary>
    [LoggerMessage(LogLevel.Error, Message = "{Message}")]
    private partial void LogError(Exception exception, string message);
}
