using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Primitives;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class YoutubeEmbeddedAndroidClient(IHttpClientFactory clientFactory, IMemoryCache cache) : YoutubeClientBase(clientFactory, cache)
{
    private string? _clientVersion;
    
    private const string ApiKey = "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";

    internal override async Task<Result<JsonNode>> GetPlayerAsync(string videoUrl, CancellationToken token = default)
    {
        _clientVersion = await GetClientVersionAsync("youtube",  "youtube_android_client_version", "20.51.39", token);

        JsonObject payload = new()
        {
            ["videoId"] = ExtractVideoId(videoUrl),
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID_EMBEDDED_PLAYER",
                    ["clientVersion"] = _clientVersion,
                    ["androidSdkVersion"] = 30,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = 0
                },
                ["thirdParty"] = new JsonObject
                {
                    ["embedUrl"] = "https://www.youtube.com"
                }
            },
            ["params"] = "8gMIAg",
            ["racyCheckOk"] = true,
            ["contentCheckOk"] = true
        };
        
        using StringContent content = new(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, $"https://www.youtube.com/youtubei/v1/player?key={ApiKey}");
        request.Headers.UserAgent.ParseAdd($"com.google.android.youtube/{_clientVersion} (Linux; U; Android 11; GB) gzip");
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Headers.AcceptEncoding.Clear(); // Force identity.
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Content = content;

        return await RetrieveJsonAsync(request, token);
    }

    internal override Task<Result<JsonNode>> SearchAsync(string query, CancellationToken token = default)
        => Task.FromResult(Result<JsonNode>.Failure("Search endpoint is not supported by ANDROID_EMBEDDED_PLAYER."));
}