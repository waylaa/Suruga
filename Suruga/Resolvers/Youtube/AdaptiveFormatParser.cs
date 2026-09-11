using System.Collections.ObjectModel;
using System.Text.Json;
using Suruga.Resolvers.Extensions;
using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers.Youtube;

internal static class AdaptiveFormatParser
{
    internal static ReadOnlyCollection<AdaptiveFormat> ParseAudioFormats(JsonElement player)
    {
        JsonElement formats = player.TraverseOrDefault("streamingData", "adaptiveFormats");

        if (formats.ValueKind is not JsonValueKind.Array)
        {
            return ReadOnlyCollection<AdaptiveFormat>.Empty;
        }

        return formats
            .EnumerateArray()
            .Where(format => format.TraverseValue<string>("mimeType")?.StartsWith("audio/") == true)
            .OrderByDescending(AudioQualityRank)
            .ThenByDescending(format => format.TraverseValue<int>("bitrate"))
            .Select(ToAdaptiveFormat)
            .ToList()
            .AsReadOnly();
    }

    private static int AudioQualityRank(JsonElement format) => format.TraverseValue<string>("audioQuality") switch
    {
        "AUDIO_QUALITY_HIGH" => 3,
        "AUDIO_QUALITY_MEDIUM" => 2,
        "AUDIO_QUALITY_LOW" => 1,
        _ => 0
    };

    private static AdaptiveFormat ToAdaptiveFormat(JsonElement format) => new
    (
        format.TraverseValue<string>("url")!,
        format.TraverseValue<string>("mimeType")!,
        format.TraverseValue<long>("contentLength")
    );
}
