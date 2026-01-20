using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Caching;
using Suruga.Primitives;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class YoutubeMusicAndroidClient : YoutubeClientBase
{
    private readonly ExpirableCache<string> _cache;

    private string? _clientVersion;

    private const string ApiKey = "AIzaSyAOghZGza2MQSZkY_zfZ370N-PUdXEo8AI";
    
    public YoutubeMusicAndroidClient(IHttpClientFactory clientFactory, IMemoryCache cache) : base(clientFactory, cache)
        => _cache = new ExpirableCache<string>(cache);

    internal override async Task<Result<JsonNode>> GetPlayerAsync(string videoUrl, CancellationToken token = default)
    {
        _clientVersion = await GetClientVersionAsync("youtube-music", "youtube_music_android_client_version", "8.50.51", token);

        JsonObject payload = new()
        {
            ["videoId"] = ExtractVideoId(videoUrl),
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID_MUSIC",
                    ["clientVersion"] = _clientVersion,
                    ["androidSdkVersion"] = 30,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utfOffsetMinutes"] = 0
                },
                ["thirdParty"] = new JsonObject
                {
                    ["embedUrl"] = "https://music.youtube.com"
                }
            },
            ["params"] = "CgIQBg",
            ["racyCheckOk"] = true,
            ["contentCheckOk"] = true
        };
        
        using StringContent content = new(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, $"https://www.youtube.com/youtubei/v1/player?key={ApiKey}");
        request.Headers.UserAgent.ParseAdd($"com.google.android.apps.youtube.music/{_clientVersion} (Linux; U; Android 11; GB) gzip");
        request.Headers.Referrer = new Uri("https://music.youtube.com/");
        request.Headers.Add("Origin", "https://music.youtube.com");
        request.Headers.AcceptEncoding.Clear(); // Force identity.
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Content = content;

        return await RetrieveJsonAsync(request, token);
    }

    internal override async Task<Result<JsonNode>> SearchAsync(string query, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<JsonNode>.Failure("Search query cannot be empty.");
        }
        
        _clientVersion = await GetClientVersionAsync("youtube-music", "youtube_music_android_client_version", "8.50.51", token);

        JsonObject payload = new()
        {
            ["query"] = query,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID_MUSIC",
                    ["clientVersion"] = _clientVersion,
                    ["androidSdkVersion"] = 30,
                    ["hl"] = "en",
                    ["gl"] = "US",
                }
            }
        };
        
        using StringContent content = new(payload.ToJsonString(), Encoding.UTF8, "application/json");

        const string url = $"https://www.youtube.com/youtubei/v1/player?key={ApiKey}";
        string userAgent = $"com.google.android.apps.youtube.music/{_clientVersion} (Linux; U; Android 11; GB) gzip";
        
        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        request.Headers.Add("Origin", "https://music.youtube.com");
        request.Content = content;

        return await RetrieveJsonAsync(request, token);
    }
}