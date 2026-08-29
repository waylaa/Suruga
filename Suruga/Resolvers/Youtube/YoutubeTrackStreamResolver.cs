using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Primitives;
using Suruga.Resolvers.Extensions;
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

		JsonElement player = root.RootElement;
		ReadOnlyCollection<AdaptiveFormat> formats = ParseAdaptiveFormats(player);

		if (formats.Count == 0)
		{
			throw new InvalidOperationException("No audio formats found.");
		}

		YoutubeStreamSource source = new(formats);
		return cache.Set(cacheKey, source, TimeSpan.FromHours(6));
	}
	
	/// <summary>
	/// Parses the adaptive audio formats from a YouTube player response.
	/// </summary>
	/// <param name="player">The player JSON response from YouTube.</param>
	/// <returns>
	/// A read-only collection of <see cref="AdaptiveFormat"/> instances sorted by audio
	/// quality and bitrate or <see langword="null"/> if no audio formats are found.
	/// </returns>
	private static ReadOnlyCollection<AdaptiveFormat> ParseAdaptiveFormats(JsonElement player)
	{
		JsonElement formats = player.TraverseOrDefault("streamingData", "adaptiveFormats");

		if (formats.ValueKind is not JsonValueKind.Array)
		{
			return ReadOnlyCollection<AdaptiveFormat>.Empty;
		}
		
		return formats
			.EnumerateArray()
			.Where(format => format.TraverseValue<string>("mimeType")?.StartsWith("audio/") == true)
			.OrderByDescending(format => format.TraverseValue<string>("audioQuality") switch
			{
				"AUDIO_QUALITY_HIGH" => 3,
				"AUDIO_QUALITY_MEDIUM" => 2,
				"AUDIO_QUALITY_LOW" => 1,
				_ => 0
			})
			.ThenByDescending(format => format.TraverseValue<int>("bitrate"))
			.Select(format => new AdaptiveFormat
			(
				format.TraverseValue<string>("url")!,
				format.TraverseValue<string>("mimeType")!,
				format.TraverseValue<long>("contentLength")
			))
			.ToList()
			.AsReadOnly();
	}
}
