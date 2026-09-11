using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;

namespace Suruga.Resolvers.Local;

internal static class LocalTrackMetadataExtractor
{
    internal static LocalTrackMetadata Extract(FormatContext formatContext, StreamInfo info, string fallbackTitle)
    {
        DictionaryView formatMeta = new(in formatContext.Metadata);
        DictionaryView streamMeta = new(in info.Stream.Metadata);

        string title = formatMeta.TryGetValue("title", out string? t) ? t : fallbackTitle;

        string artist = formatMeta.TryGetValue("artist", out string? a)
            ? a
            : streamMeta.TryGetValue("album_artist", out string? albumArtist) ? albumArtist : "Unknown";

        long durationUnits = formatContext.Duration;

        if (durationUnits <= 0 && streamMeta.TryGetValue("DURATION", out string? dur))
        {
            long.TryParse(dur, out durationUnits);
        }

        TimeSpan? duration = TimeSpan.FromSeconds(durationUnits / (double)Constants.AV_TIME_BASE);
        return new LocalTrackMetadata(title, artist, duration);
    }
    
    internal readonly record struct LocalTrackMetadata(string Title, string Artist, TimeSpan? Duration);
}
