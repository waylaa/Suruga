using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class YoutubeAndroidVrClient(HttpClient client) : YoutubeClient(client)
{
    internal override Task<Result<JsonDocument>> GetDynamicPlaylistAsync(string playlistId, string initialVideoId, int playlistIndex, string? continuationToken, CancellationToken token = default)
        => Task.FromResult(Result<JsonDocument>.Failure(new NotSupportedException()));

    internal override async Task<Result<JsonDocument>> GetPlaylistAsync(string playlistId, string? continuationToken, CancellationToken token = default)
    {
        string visitorData = await GetVisitorDataAsync(token);

        JsonObject body = new()
        {
            ["browseId"] = $"VL{playlistId}",
            ["continuation"] = continuationToken,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID_VR",
                    ["clientVersion"] = "1.60.19",
                    ["deviceMake"] = "Oculus",
                    ["deviceModel"] = "Quest 3",
                    ["osName"] = "Android",
                    ["osVersion"] = "12L",
                    ["platform"] = "MOBILE",
                    ["visitorData"] = visitorData,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = (int)DateTimeOffset.Now.Offset.TotalMinutes
                }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/browse");
        request.Headers.UserAgent.ParseAdd("com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip");
        request.Headers.Add("X-Goog-Visitor-Id", visitorData);
        request.Headers.Add("X-Youtube-Client-Name", "28"); // 28 -> ANDROID_VR client.
        request.Headers.Add("X-Youtube-Client-Version", "1.60.19");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await Client.SendAsync(request, token);
        
        try
        {
            response.EnsureSuccessStatusCode();
            
            await using Stream jsonStream = await response.Content.ReadAsStreamAsync(token);
            return await JsonDocument.ParseAsync(jsonStream, cancellationToken: token);
        }
        catch (Exception ex)
        {
            return Result<JsonDocument>.Failure(ex);
        }
    }

    internal override async Task<Result<JsonDocument>> SearchAsync(string query, CancellationToken token = default)
    {
        string visitorData = await GetVisitorDataAsync(token);

        JsonObject body = new()
        {
            ["query"] = query,
            ["params"] = "EgIQAQ%3D%3D", // Video-only filter.
            ["continuation"] = null,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID_VR",
                    ["clientVersion"] = "1.60.19",
                    ["deviceMake"] = "Oculus",
                    ["deviceModel"] = "Quest 3",
                    ["osName"] = "Android",
                    ["osVersion"] = "12L",
                    ["platform"] = "MOBILE",
                    ["visitorData"] = visitorData,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = (int)DateTimeOffset.Now.Offset.TotalMinutes
                }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/search");
        request.Headers.UserAgent.ParseAdd("com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip");
        request.Headers.Add("X-Goog-Visitor-Id", visitorData);
        request.Headers.Add("X-Youtube-Client-Name", "28"); // 28 -> ANDROID_VR client.
        request.Headers.Add("X-Youtube-Client-Version", "1.60.19");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await Client.SendAsync(request, token);
        
        try
        {
            response.EnsureSuccessStatusCode();
            
            await using Stream jsonStream = await response.Content.ReadAsStreamAsync(token);
            return await JsonDocument.ParseAsync(jsonStream, cancellationToken: token);
        }
        catch (Exception ex)
        {
            return Result<JsonDocument>.Failure(ex);
        }
    }
}
