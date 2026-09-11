using System.Text.Json;
using System.Text.Json.Nodes;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal abstract class YoutubeClient(HttpClient client, InnertubeClientProfile profile)
{
    private readonly InnertubeRequestExecutor _executor = new(client);
    
    private string? _visitorData;

    internal abstract Task<Result<JsonDocument>> GetDynamicPlaylistAsync
    (
        string playlistId,
        string initialVideoId,
        int playlistIndex,
        string? continuationToken,
        CancellationToken token = default
    );
    
    internal abstract Task<Result<JsonDocument>> GetPlaylistAsync(string playlistId, string? continuationToken, CancellationToken token = default);
    
    internal abstract Task<Result<JsonDocument>> SearchAsync(string query, CancellationToken token = default);

    protected async Task<Result<JsonDocument>> ExecuteAsync(string url, JsonObject bodyWithoutContext, CancellationToken token)
    {
        string visitorData = await GetVisitorDataAsync(token);
        bodyWithoutContext["context"] = InnertubeContextBuilder.Build(profile, visitorData);

        return await _executor.PostAsync(url, profile, visitorData, bodyWithoutContext, token);
    }

    private async Task<string> GetVisitorDataAsync(CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(_visitorData))
        {
            return _visitorData;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, "https://www.youtube.com/sw.js_data");
        request.Headers.Accept.ParseAdd("application/json");

        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        string jsonString = await response.Content.ReadAsStringAsync(token);

        if (jsonString.StartsWith(")]}'"))
        {
            jsonString = jsonString[4..];
        }

        using JsonDocument json = JsonDocument.Parse(jsonString);
        string value = json.RootElement[0][2][0][0][13].GetString() ?? string.Empty;

        return _visitorData = value;
    }
}
