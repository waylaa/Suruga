using System.Collections.Specialized;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Primitives;
using System.Web;
using Suruga.Extensions;
using Suruga.Resolvers.Youtube.Clients;

namespace Suruga.Resolvers.Youtube;

/// <summary>
/// Resolves YouTube video IDs, URLs, playlists, and search queries into
/// <see cref="Track"/> instances.
/// </summary>
/// <param name="clientRouter">The client router.</param>
/// <param name="playerClient">The player client.</param>
/// <param name="cache">The underlying cache.</param>
internal sealed class YoutubeTrackResolver
(
    YoutubeClientRouter clientRouter,
    InvidiousCompanionClient playerClient,
    IMemoryCache cache
) : ITrackResolver
{
    public TrackPlatform Platform => TrackPlatform.Youtube;

    public async ValueTask<Result<TrackSet>> ResolveAsync(Input input, TrackRequestContext requestedBy, CancellationToken token = default)
    {
        return input.Type switch
        { 
            InputType.YoutubeVideoId or InputType.YoutubeVideoUrl => await ResolveVideoAsync(input, requestedBy, token),
            InputType.YoutubePlaylist => await ResolvePlaylistAsync(input, requestedBy, token),
            InputType.YoutubeSearch => await ResolveSearchResultAsync(input, requestedBy, token),
            _ => TrackSet.Empty
        };
    } 
    
    /// <summary>
    /// Resolves a single YouTube video into a <see cref="Track"/>.
    /// </summary>
    /// <param name="input">The parsed input containing the video ID or URI.</param>
    /// <param name="requestedBy"></param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>
    /// A task that completes with a read-only list containing the
    /// resolved track.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the video is not playable or contains no audio formats.
    /// </exception>
    private async Task<Result<TrackSet>> ResolveVideoAsync(Input input, TrackRequestContext requestedBy, CancellationToken token)
    {
        string videoId = input.Value;
        
        if (input.Type is InputType.YoutubeVideoUrl)
        {
            videoId = HttpUtility.ParseQueryString(new Uri(input.Value).Query)["v"]!;
        }

        string cacheKey = $"yt-track:{videoId}";
        
        if (cache.TryGetValue(cacheKey, out TrackSet? cachedSet))
        {
            return cachedSet!;
        }
        
        Result<JsonDocument> responseResult = await playerClient.GetPlayerAsync(videoId, token);

        if (!responseResult.TryGetValue(out JsonDocument? root))
        {
            return responseResult.MapFailure<TrackSet>();
        }

        JsonElement player = root.RootElement;
        
        // Validate playability.
        string? status = player.TraverseValue<string>("playabilityStatus", "status");

        if (status != "OK")
        {
            return TrackSet.Empty;
        }

        JsonElement? videoDetails = player.TraverseOrDefault("videoDetails");

        if (videoDetails is not JsonElement details)
        {
            return TrackSet.Empty;
        }
        
        string videoUrl = $"https://www.youtube.com/watch?v={videoId}";

        Track descriptor = new()
        {
            Platform = TrackPlatform.Youtube,
            Id = videoId,
            Uri = videoUrl,
            Title = details.TraverseValue<string>("title") ?? "Unknown",
            Author = details.TraverseValue<string>("author") ?? "Unknown",
            ThumbnailUri = details
                .TraverseOrDefault("thumbnail", "thumbnails")
                .GetElementAtOrDefault(static element => element.GetArrayLength() - 1) // Get last thumbnail index.
                .TraverseValue<string>("url"),
            Duration = details.TraverseOrDefault("lengthSeconds").GetTimeSpanSeconds(),
            StartPosition = ParseStartTimestamp(videoUrl),
            RequestedBy = requestedBy
        };
        
        return cache.Set(cacheKey, new TrackSet([descriptor]));
    }

    /// <summary>
    /// Resolves a YouTube playlist into a read-only collection of <see cref="Track"/>
    /// references.
    /// </summary>
    /// <param name="input">The parsed input containing the playlist URI.</param>
    /// <param name="requestedBy"></param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>A task that completes with a read-only list of tracks from the playlist.</returns>
    private async Task<Result<TrackSet>> ResolvePlaylistAsync(Input input, TrackRequestContext requestedBy, CancellationToken token)
    {
        string cacheKey = $"yt-playlist:{input.Value}";

        if (cache.TryGetValue(cacheKey, out TrackSet? cachedSet))
        {
            return Result<TrackSet>.Success(cachedSet!);
        }
        
        NameValueCollection query = HttpUtility.ParseQueryString(new Uri(input.Value).Query);
        string playlistId = query["list"]!;
        string? initialVideoId = query["v"];

        switch (playlistId)
        {
            case string when playlistId.StartsWith("LL"):
                return Result<TrackSet>.Failure(new Exception("Cannot play liked videos."));
            
            case string when playlistId.StartsWith("WL"):
                return Result<TrackSet>.Failure(new Exception("Cannot play watch-later videos."));
            
            case string when playlistId.StartsWith("RD"):
            case string when playlistId.StartsWith("RDMM"):
            case string when playlistId.StartsWith("RDCM"):
            case string when playlistId.StartsWith("RDAMVM"):
            case string when playlistId.StartsWith("RDCLAK"):
                return Result<TrackSet>.Failure(new Exception("This playlist is unsupported."));
        }
        
        List<Track> tracks = [];
        string? continuationToken = null;
        int playlistIndex = 0;
        int enumeratedVideoCount = 0;
        int totalVideoCount = 0;
        
        do
        {
            Result<JsonDocument> responseResult = await clientRouter.RequestAsync(async (client, ct) =>
            {
                return playlistId switch
                {
                    string when playlistId.StartsWith("PL") || playlistId.StartsWith("UU") => 
                        await client.GetPlaylistAsync(playlistId, continuationToken, ct),
                    
                    string when playlistId.StartsWith("OL") && !string.IsNullOrWhiteSpace(initialVideoId) => await client
                        .GetDynamicPlaylistAsync(playlistId, initialVideoId, playlistIndex, continuationToken, ct),
                    
                    _ => new InvalidOperationException("Could not resolve YouTube playlist.")
                };
            }, token);

            if (!responseResult.TryGetValue(out JsonDocument? response))
            {
                return responseResult.MapFailure<TrackSet>();
            }
            
            JsonElement root = response.RootElement;
            JsonElement videos = default;

            if (IsStaticPlaylist(playlistId))
            {
                if (string.IsNullOrEmpty(continuationToken))
                {
                    // Initial playlist response.
                    continuationToken = root
                        .TraverseOrDefault("contents", "singleColumnBrowseResultsRenderer", "tabs")
                        .GetElementAtOrDefault(0)
                        .TraverseOrDefault("tabRenderer", "content", "sectionListRenderer", "contents")
                        .GetElementAtOrDefault(0)
                        .TraverseOrDefault("playlistVideoListRenderer", "continuations")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<string>("nextContinuationData", "continuation");
                    
                    totalVideoCount = root
                        .TraverseOrDefault("header", "playlistHeaderRenderer", "numVideosText", "runs")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<int>("text");
                    
                    videos = root
                        .TraverseOrDefault("contents", "singleColumnBrowseResultsRenderer", "tabs")
                        .GetElementAtOrDefault(0)
                        .TraverseOrDefault("tabRenderer", "content", "sectionListRenderer", "contents")
                        .GetElementAtOrDefault(0)
                        .TraverseOrDefault("playlistVideoListRenderer", "contents");
                }
                else
                {
                    // Playlist response after a continuation.
                    continuationToken = root
                        .TraverseOrDefault("continuationContents", "playlistVideoListContinuation", "continuations")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<string>("nextContinuationData", "continuation");
                    
                    videos = root.TraverseOrDefault("continuationContents", "playlistVideoListContinuation", "contents");
                }
            }
            else if (IsDynamicPlaylist(playlistId))
            {
                continuationToken = root
                    .TraverseOrDefault("contents", "singleColumnWatchNextResults", "results", "results", "continuations")
                    .GetElementAtOrDefault(0)
                    .TraverseValue<string>("nextContinuationData", "continuation");
                
                totalVideoCount = root
                    .TraverseValue<int>("contents", "singleColumnWatchNextResults", "playlist", "playlist", "totalVideos");
                
                videos = root.TraverseOrDefault("contents", "singleColumnWatchNextResults", "playlist", "playlist", "contents");
            }
            
            if (videos.ValueKind is not JsonValueKind.Array)
            {
                return TrackSet.Empty;
            }

            // This does not matter if it is null on static playlists.
            playlistIndex = root
                .TraverseValue<int>("contents", "singleColumnWatchNextResults", "playlist", "playlist", "currentIndex");
            
            int currentPageVideoCount = videos.GetArrayLength();
            enumeratedVideoCount += currentPageVideoCount;

            foreach (JsonElement item in videos.EnumerateArray())
            {
                JsonElement video = default;
                
                if (IsStaticPlaylist(playlistId))
                {
                    video = item.TraverseOrDefault("playlistVideoRenderer");
                }
                else if (IsDynamicPlaylist(playlistId))
                {
                    video = item.TraverseOrDefault("playlistPanelVideoRenderer");
                }

                if (video.ValueKind is not JsonValueKind.Object)
                {
                    continue;
                }

                bool isPlayable = video.TraverseValue<bool>("isPlayable");

                if (!isPlayable)
                {
                    continue;
                }

                string? videoId = video.TraverseValue<string>("videoId");

                if (string.IsNullOrWhiteSpace(videoId))
                {
                    continue;
                }
                
                tracks.Add(new Track
                {
                    Platform = TrackPlatform.Youtube,
                    Id = videoId,
                    Uri = $"https://www.youtube.com/watch?v={videoId}",
                    Title = video.TraverseOrDefault("title", "runs")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<string>("text") ?? "Unknown",
                    Author = IsStaticPlaylist(playlistId)
                        ? root.TraverseOrDefault("header", "playlistHeaderRenderer", "ownerText", "runs")
                              .GetElementAtOrDefault(0)
                              .TraverseValue<string>("text")
                          ?? video.TraverseOrDefault("shortBylineText", "runs")
                              .GetElementAtOrDefault(0)
                              .TraverseValue<string>("text")
                          ?? "Unknown"
                        : video.TraverseOrDefault("longBylineText", "runs")
                              .GetElementAtOrDefault(0)
                              .TraverseValue<string>("text")
                          ?? video.TraverseOrDefault("shortBylineText", "runs")
                              .GetElementAtOrDefault(0)
                              .TraverseValue<string>("text")
                          ?? "Unknown",
                    ThumbnailUri = video
                        .TraverseOrDefault("thumbnail", "thumbnails")
                        .GetElementAtOrDefault(static element => element.GetArrayLength() - 1) // Get last thumbnail index.
                        .TraverseValue<string>("url"),
                    Duration = IsStaticPlaylist(playlistId)
                        ? video.TraverseOrDefault("lengthSeconds").GetTimeSpanSeconds()
                        : video.TraverseOrDefault("lengthText", "runs")
                            .GetElementAtOrDefault(0)
                            .TraverseOrDefault("text")
                            .GetTimeSpan(),
                    RequestedBy = requestedBy
                });
            }
        } while (continuationToken is not null && totalVideoCount != enumeratedVideoCount);

        return cache.Set(cacheKey, new TrackSet(tracks));
    }

    /// <summary>
    /// Resolves a YouTube search query into a collection of <see cref="Track"/>
    /// references.
    /// </summary>
    /// <param name="input">The parsed input containing the search query.</param>
    /// <param name="requestedBy"></param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>
    /// A task that completes with a read-only list of tracks from the search results.
    /// </returns>
    private async Task<Result<TrackSet>> ResolveSearchResultAsync(Input input, TrackRequestContext requestedBy, CancellationToken token)
    {
        string query = input.Value;
        string cacheKey = $"yt-search:{query}";

        if (cache.TryGetValue(cacheKey, out TrackSet? cachedSet))
        {
            return cachedSet!;
        }

        Result<JsonDocument> responseResult = await clientRouter
            .RequestAsync(async (client, ct) => await client.SearchAsync(query, ct), token);

        if (!responseResult.TryGetValue(out JsonDocument? response))
        {
            return TrackSet.Empty;
        }

        JsonElement root = response.RootElement;

        JsonElement[]? contents = root
            .TraverseOrDefault("contents", "sectionListRenderer")
            .GetElementAtOrDefault(0)
            .TraverseOrDefault("itemSectionRenderer", "contents")
            .EnumerateArrayOrNull()
            ?.Take(25)
            .ToArray() ?? root
            .TraverseOrDefault("contents", "sectionListRenderer")
            .TraverseOrDefault("contents")
            .EnumerateArrayOrNull()
            ?.FirstOrDefault()
            .TraverseOrDefault("itemSectionRenderer", "contents")
            .EnumerateArrayOrNull()
            ?.Take(25)
            .ToArray();

        if (contents is null)
        {
            return TrackSet.Empty;
        }

        List<Track> tracks = [];

        foreach (JsonElement item in contents)
        {
            JsonElement video = item.TraverseOrDefault("compactVideoRenderer");

            if (video.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            string? videoId = video.TraverseValue<string>("videoId");

            if (string.IsNullOrWhiteSpace(videoId))
            {
                continue;
            }
            
            tracks.Add(new Track
            {
                Platform = TrackPlatform.Youtube,
                Id = videoId,
                Uri = $"https://www.youtube.com/watch?v={videoId}",
                Title = video
                    .TraverseOrDefault("title", "runs")
                    .GetElementAtOrDefault(0)
                    .TraverseValue<string>("text") ?? "Unknown title",
                Author = video.TraverseOrDefault("shortBylineText", "runs")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<string>("text") 
                    ?? video.TraverseOrDefault("longBylineText", "runs")
                        .GetElementAtOrDefault(0)
                        .TraverseValue<string>("text")
                    ?? "Unknown author",
                RequestedBy = requestedBy
            });
        }

        return cache.Set(cacheKey, new TrackSet(tracks));
    }
    
    /// <summary>
    /// Extracts the start timestamp from a YouTube URI query string.
    /// </summary>
    /// <param name="videoUrl">The full YouTube video URI.</param>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the start time, or
    /// <see langword="null"/> if not specified.
    /// </returns>
    private static TimeSpan? ParseStartTimestamp(string videoUrl)
    {
        NameValueCollection queries = HttpUtility.ParseQueryString(new Uri(videoUrl).Query);
        
        if (!int.TryParse(queries["t"] ?? queries["start"], out int timestampInSeconds))
        {
            return null;
        }
        
        return TimeSpan.FromSeconds(timestampInSeconds);
    }

    private static bool IsDynamicPlaylist(string playlistId)
        => playlistId.StartsWith("OL");

    private static bool IsStaticPlaylist(string playlistId)
        => playlistId.StartsWith("PL") || playlistId.StartsWith("UU");
}
