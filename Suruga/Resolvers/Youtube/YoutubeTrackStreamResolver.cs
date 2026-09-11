using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Primitives;
using Suruga.Resolvers.Primitives;
using Suruga.Resolvers.Sources;
using Suruga.Resolvers.Youtube.Clients;

namespace Suruga.Resolvers.Youtube;

internal sealed class YoutubeTrackStreamResolver(InvidiousCompanionClient client, IMemoryCache cache) : ITrackStreamResolver
{
    public TrackPlatform Platform => TrackPlatform.Youtube;

    public async ValueTask<StreamSource> ResolveStreamUriAsync(Track descriptor, CancellationToken token = default)
    {
        string cacheKey = $"yt-stream:{descriptor.Id}";

        if (cache.TryGetValue(cacheKey, out YoutubeStreamSource? cachedSource))
        {
            return cachedSource!;
        }

        Result<JsonDocument> responseResult = await client.GetPlayerAsync(descriptor.Id, token);

        if (!responseResult.TryGetValue(out JsonDocument? root))
        {
            throw new InvalidOperationException("Failed to get video player.");
        }

        ReadOnlyCollection<AdaptiveFormat> formats = AdaptiveFormatParser.ParseAudioFormats(root.RootElement);

        if (formats.Count == 0)
        {
            throw new InvalidOperationException("No audio formats found.");
        }

        return cache.Set(cacheKey, new YoutubeStreamSource(formats), TimeSpan.FromHours(6));
    }
}
