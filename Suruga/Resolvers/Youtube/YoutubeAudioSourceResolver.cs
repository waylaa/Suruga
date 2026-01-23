using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Caching;
using Suruga.Primitives;
using Suruga.Resolvers.Abstractions;
using Suruga.Resolvers.Inputs;
using Suruga.Resolvers.Youtube.Clients;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Resolvers.Youtube;

internal sealed class YoutubeAudioSourceResolver(CompositeYoutubeClient youtube, IMemoryCache cache) : IAudioSourceResolver
{
    private readonly ExpirableCache<AudioSource> _sourceCache = new(cache);
    
    public async Task<Result<AudioSource>> ResolveAsync(string input, CancellationToken token = default)
    {
        if (!InputValidator.TryValidate(input, out InputType? type))
        {
            return Result<AudioSource>.Failure("Input is not a valid Youtube video URL/Query/ID.");
        }

        return type switch
        {
            InputType.YoutubeId or InputType.YoutubeUrl => await DeserializeSingleTrackAsync(input, type.GetValueOrDefault(), token),
            InputType.YoutubeQuery => await DeserializeTracksAsync(input, token),
            _ => Result<AudioSource>.Failure("Unsupported Youtube input type.")
        };
    }

    private async Task<Result<AudioSource>> DeserializeSingleTrackAsync(string input, InputType type, CancellationToken token)
    {
        string cacheKey = $"yt:{input}";

        if (_sourceCache.TryGet(cacheKey, out AudioSource? source))
        {
            return Result<AudioSource>.Success(source);
        }

        string videoUrl = input; // Assume input is a url first.

        if (type is InputType.YoutubeId)
        {
            videoUrl = $"https://www.youtube.com/watch?v={input}";
        }

        Result<(JsonNode Response, YoutubeClientBase Client)> playerResult = await youtube.GetPlayerAsync(videoUrl, token);
        
        if (!playerResult.TryGetValue(out (JsonNode Response, YoutubeClientBase Client) tuple))
        {
            return Result<AudioSource>.Failure(playerResult.Error);
        }

        YoutubeClientBase client = tuple.Client;
        JsonNode player = tuple.Response;
        
        Result playabilityResult = ValidatePlayability(player);

        if (!playabilityResult.IsSuccessful)
        {
            return Result<AudioSource>.Failure(playabilityResult.Error);
        }
        
        JsonNode? videoDetails = player["videoDetails"];

        if (videoDetails is null)
        {
            return Result<AudioSource>.Failure("Video details response is null.");
        }

        bool? isPrivate = (bool?)videoDetails["isPrivate"];

        if (isPrivate is null or true) // Assume null bool is a private video.
        {
            return Result<AudioSource>.Failure("Video is private.");
        }

        string? id = videoDetails["videoId"]?.ToString();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<AudioSource>.Failure("Video ID is null or empty.");
        }

        string title = videoDetails["title"]?.ToString() ?? "Unknown youtube track";
        string author = videoDetails["author"]?.ToString() ?? "Unknown";
        string? duration = videoDetails["lengthSeconds"]?.ToString();
        string? thumbnailUrl = videoDetails["thumbnail"]?["thumbnails"]?.AsArray().LastOrDefault()?["url"]?.ToString(); // The last thumbnail usually is the highest resolution.

        JsonNode? audioFormat = SelectBestAudioFormat(player);

        if (audioFormat is null)
        {
            return Result<AudioSource>.Failure("Adaptive format response is null.");
        }

        string? streamUrl = audioFormat["url"]?.ToString();

        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            return Result<AudioSource>.Failure("Stream URL is null or empty.");
        }

        NameValueCollection queries = HttpUtility.ParseQueryString(new Uri(streamUrl).Query);
        string? expiryStr = queries["expire"];

        if (!long.TryParse(expiryStr, out long unixTimestamp))
        {
            return Result<AudioSource>.Failure("Failed to parse stream URL expiry.");
        }

        TimeSpan streamUrlExpiry = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp) - DateTimeOffset.UtcNow;

        source = AudioSource.FromSingle(AudioPlatform.Youtube, new AudioTrack
        {
            Platform = AudioPlatform.Youtube,
            Title = title,
            Url = videoUrl,
            StreamUrl = streamUrl,
            Author = author,
            ThumbnailUrl = thumbnailUrl,
            Duration = TimeSpan.TryParse(duration, out TimeSpan trackDuration) ? trackDuration : null,
            Callback = () => DeserializeSingleTrackAsync(input, type, token),
            CallbackInfo = new Dictionary<string, object>
            {
                ["Client"] = client,
                ["Expiry"] = streamUrlExpiry
            }
        });

        _sourceCache.Set(cacheKey, source, streamUrlExpiry);
        return Result<AudioSource>.Success(source);
    }

    private async Task<Result<AudioSource>> DeserializeTracksAsync(string query, CancellationToken token)
    {
        string cacheKey = $"yt:{query}";

        if (_sourceCache.TryGet(cacheKey, out AudioSource? source))
        {
            return Result<AudioSource>.Success(source);
        }

        Result<JsonNode> responseResult = await youtube.SearchAsync(query, token);

        if (!responseResult.TryGetValue(out JsonNode? root))
        {
            return Result<AudioSource>.Failure(responseResult.Error);
        }

        ReadOnlyCollection<JsonNode?> searchResults = root
            ["contents"]
            ?["twoColumnSearchResultsRenderer"]
            ?["primaryContents"]
            ?["sectionListRenderer"]
            ?["contents"]?.AsArray()
            .SelectMany(section => section?["itemSectionRenderer"]?["contents"]?.AsArray() ?? [])
            .Select(item => item?["videoRenderer"])
            .Take(25) // 25 -> Max discord autocomplete result limit.
            .ToList()
            .AsReadOnly() ?? ReadOnlyCollection<JsonNode?>.Empty;
        
        List<AudioSource> sources = new(searchResults.Count);

        foreach (JsonNode? video in searchResults)
        {
            string? videoId = video?["videoId"]?.ToString();

            if (string.IsNullOrWhiteSpace(videoId))
            {
                continue;
            }

            if (!InputValidator.TryValidate(videoId, out InputType? type))
            {
                continue;
            }
            
            Result<AudioSource> sourceResult = await DeserializeSingleTrackAsync(videoId, type.GetValueOrDefault(), token);

            if (!sourceResult.TryGetValue(out AudioSource? newSource))
            {
                continue;
            }

            sources.Add(newSource);
        }

        ReadOnlyCollection<AudioTrack> tracks = sources
            .SelectMany(x => x.Tracks)
            .ToList()
            .AsReadOnly();

        source = AudioSource.FromPlaylist(AudioPlatform.Youtube, tracks);
        _sourceCache.Set(cacheKey, source, TimeSpan.FromMinutes(10));

        return Result<AudioSource>.Success(source);
    }

    private static Result ValidatePlayability(JsonNode player)
    {
        string? status = player["playabilityStatus"]?["status"]?.ToString();

        if (status is null)
        {
            return Result.Failure("Missing playability status.");
        }
        
        string? reason = player["playabilityStatus"]?["reason"]?.ToString();

        if (status != "OK" && reason is null)
        {
            return Result.Failure("Missing playability reason.");
        }

        return status switch
        {
            "OK" => Result.Success,
            "LOGIN_REQUIRED" => Result.Failure("Login required."),
            "UNPLAYABLE" => Result.Failure("Video is unplayable."),
            _ => Result.Failure($"{status}: {reason}")
        };
    }

    private static JsonNode? SelectBestAudioFormat(JsonNode player)
    {
        return player["streamingData"]?["adaptiveFormats"]?.AsArray()
            .Where(f => f?["mimeType"]?.ToString().Contains("audio") == true)
            .OrderByDescending(f => f?["audioQuality"]?.ToString() switch
            {
                "AUDIO_QUALITY_MEDIUM" => 2,
                "AUDIO_QUALITY_LOW" => 3,
                _ => 0
            })
            .ThenByDescending(f => (int?)f?["bitrate"] ?? 0)
            .FirstOrDefault();
    }
}
