using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class InnertubeRequestExecutor(HttpClient client)
{
    internal Task<Result<JsonDocument>> PostAsync(string url, InnertubeClientProfile profile, string visitorData, JsonObject body, CancellationToken token)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, url);
        
        ApplyHeaders(request, profile, visitorData);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        return HttpJsonDocumentReader.SendAsync(client, request, token);
    }

    private static void ApplyHeaders(HttpRequestMessage request, InnertubeClientProfile profile, string visitorData)
    {
        request.Headers.UserAgent.ParseAdd(profile.UserAgent);
        request.Headers.Add("X-Goog-Visitor-Id", visitorData);
        request.Headers.Add("X-Youtube-Client-Name", profile.ClientNameHeaderValue);
        request.Headers.Add("X-Youtube-Client-Version", profile.ClientVersion);
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.youtube.com/");
    }
}
