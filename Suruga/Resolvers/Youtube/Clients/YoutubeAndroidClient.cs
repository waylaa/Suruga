using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Primitives;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class YoutubeAndroidClient(IHttpClientFactory clientFactory, IMemoryCache cache) : YoutubeClientBase(clientFactory, cache)
{
    private string UserAgent => $"com.google.android.youtube/{_clientVersion} (Linux; U; Android 12) gzip";
    
    private string? _clientVersion;

    private const string ApiKey = "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";

    internal override async Task<Result<JsonNode>> GetPlayerAsync(string videoUrl, CancellationToken token = default)
    {
        _clientVersion = await GetClientVersionAsync("youtube", "youtube_android_client_version", "20.51.39", token);
        string visitorData = await GetVisitorDataAsync(UserAgent, token) ?? throw new InvalidOperationException("Failed to get visitor data.");
        
        JsonObject payload = new()
        {
            ["videoId"] = ExtractVideoId(videoUrl),
            ["contentCheckOk"] = true,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID",
                    ["clientVersion"] = _clientVersion,
                    ["osName"] = "Android",
                    ["osVersion"] = "12",
                    ["platform"] = "MOBILE",
                    ["visitorData"] = visitorData,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = 0
                },
            },
        };

        using StringContent content = new(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, $"https://www.youtube.com/youtubei/v1/player?key={ApiKey}");
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
        request.Headers.Add("Origin", "https://www.youtube.com");
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
        
        _clientVersion = await GetClientVersionAsync("youtube", "youtube_android_client_version", "20.51.39", token);

        JsonObject payload = new()
        {
            ["query"] = query.TrimStart().TrimEnd(),
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID",
                    ["clientVersion"] = _clientVersion,
                    ["androidSdkVersion"] = 30,
                    ["hl"] = "en",
                    ["gl"] = "US"
                }
            },
            ["params"] = "EgIQAQ==" // Video-only filter.
        };
        
        using StringContent content = new(payload.ToJsonString(), Encoding.UTF8, "application/json");
        
        const string url = $"https://www.youtube.com/youtubei/v1/search?key={ApiKey}";
        
        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Headers.AcceptEncoding.Clear(); // Force identity.
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Content = content;
        
        return await RetrieveJsonAsync(request, token);
    }
}