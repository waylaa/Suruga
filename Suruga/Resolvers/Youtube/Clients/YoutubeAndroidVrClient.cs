using System.Text.Json;
using System.Text.Json.Nodes;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class YoutubeAndroidVrClient(HttpClient client) : YoutubeClient(client, InnertubeClientProfiles.AndroidVr)
{
    internal override Task<Result<JsonDocument>> GetDynamicPlaylistAsync
    (
        string playlistId,
        string initialVideoId,
        int playlistIndex,
        string? continuationToken,
        CancellationToken token = default
    ) => Task.FromResult(Result<JsonDocument>.Failure(new NotSupportedException()));

    internal override Task<Result<JsonDocument>> GetPlaylistAsync(string playlistId, string? continuationToken, CancellationToken token = default)
    {
        return ExecuteAsync("https://www.youtube.com/youtubei/v1/browse", new JsonObject
        {
            ["browseId"] = $"VL{playlistId}",
            ["continuation"] = continuationToken
        }, token);
    }

    internal override Task<Result<JsonDocument>> SearchAsync(string query, CancellationToken token = default)
    {
        return ExecuteAsync("https://www.youtube.com/youtubei/v1/search", new JsonObject
        {
            ["query"] = query,
            ["params"] = "EgIQAQ%3D%3D",
            ["continuation"] = null
        }, token);
    }
}
