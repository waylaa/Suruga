using System.Text.Json;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

/// <summary>
/// Provides a base implementation for Innertube clients.
/// </summary>
/// <param name="client">The underlying HTTP client.</param>
internal abstract class YoutubeClient(HttpClient client)
{
    /// <summary>
    /// Gets the HTTP client used for making requests.
    /// </summary>
    protected readonly HttpClient Client = client;

    private readonly AsyncLock _lock = new();

    private string? _visitorData;
    
    /// <summary>
    /// Retrieves a dynamic playlist (e.g, a mix or radio playlist) from YouTube.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="initialVideoId">The starting video ID for the playlist.</param>
    /// <param name="playlistIndex">The index position within the playlist.</param>
    /// <param name="continuationToken">A token for fetching subsequent pages, if any.</param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>A task that completes with the JSON response containing playlist data.</returns>
    internal abstract Task<Result<JsonDocument>> GetDynamicPlaylistAsync(string playlistId, string initialVideoId, int playlistIndex, string? continuationToken, CancellationToken token = default);

    /// <summary>
    /// Retrieves a standard (user-made) playlist from YouTube.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="continuationToken">A token for fetching subsequent pages, if any.</param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>A task that completes with the JSON response containing playlist data.</returns>
    internal abstract Task<Result<JsonDocument>> GetPlaylistAsync(string playlistId, string? continuationToken, CancellationToken token = default);
    
    /// <summary>
    /// Performs a search query against YouTube.
    /// </summary>
    /// <param name="query">The search term to query.</param>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>A task that completes with the JSON response containing search results.</returns>
    internal abstract Task<Result<JsonDocument>> SearchAsync(string query, CancellationToken token = default);
    
    /// <summary>
    /// Obtains the visitor data string required for authenticated (partially) Innertube
    /// requests.
    /// </summary>
    /// <param name="token">A token that can cancel the operation.</param>
    /// <returns>A task that completes with the visitor data string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the visitor data cannot be parsed from the response.
    /// </exception>
    protected async Task<string> GetVisitorDataAsync(CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(_visitorData))
        {
            return _visitorData;
        }
        
        using (await _lock.EnterScopeAsync(token))
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "https://www.youtube.com/sw.js_data");
            request.Headers.Accept.ParseAdd("application/json");

            using HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync(token);

            if (jsonString.StartsWith(")]}'"))
            {
                jsonString = jsonString[4..];
            }

            using JsonDocument json = JsonDocument.Parse(jsonString); 

            // This is just an ordered (but unstructured) blob of data.
            // (https://github.com/Tyrrrz/YoutubeExplode/blob/prime/YoutubeExplode/Videos/VideoController.cs#L18)
            string value = json.RootElement[0][2][0][0][13].GetString() ?? string.Empty;

            return _visitorData = value;
        }
    }
}
