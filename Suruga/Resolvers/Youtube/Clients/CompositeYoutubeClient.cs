using System.Text.Json.Nodes;
using Suruga.Primitives;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class CompositeYoutubeClient(IEnumerable<YoutubeClientBase> clients)
{
    internal async Task<Result<(JsonNode Response, YoutubeClientBase Client)>> GetPlayerAsync(string videoUrl, CancellationToken token = default)
    {
        Exception? lastError = null;
        
        foreach (YoutubeClientBase client in clients)
        {
            Result<JsonNode> result = await client.GetPlayerAsync(videoUrl, token);

            if (result.TryGetValue(out JsonNode? response))
            {
                return Result<(JsonNode, YoutubeClientBase)>.Success((response, client));
            }
            
            lastError = result.Error;
        }

        return Result<(JsonNode, YoutubeClientBase)>.Failure(lastError!);
    }

    internal async Task<Result<JsonNode>> SearchAsync(string query, CancellationToken token = default)
    {
        foreach (YoutubeClientBase client in clients)
        {
            if (client is YoutubeEmbeddedAndroidClient)
            {
                continue;
            }
            
            Result<JsonNode> result = await client.SearchAsync(query, token);

            if (result.IsSuccessful)
            {
                return result;
            }
        }
        
        return Result<JsonNode>.Failure($"No youtube client could handle '{query}'.");
    }
}