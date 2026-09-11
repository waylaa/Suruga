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
    
    public ValueTask<Result<TrackSet>> ResolveAsync(Input input, TrackRequestContext requestedBy, CancellationToken token = default)
    {
        string filePath = input.Value;
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            using FormatContext formatContext = new(filePath);
            StreamInfo info = formatContext.GetStream();

            LocalTrackMetadataExtractor.LocalTrackMetadata metadata = LocalTrackMetadataExtractor.Extract(formatContext, info, fileName);

            Track track = new(TrackPlatform.Local, fileName, filePath)
            {
                Title = metadata.Title,
                Author = metadata.Artist,
                Duration = metadata.Duration,
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
