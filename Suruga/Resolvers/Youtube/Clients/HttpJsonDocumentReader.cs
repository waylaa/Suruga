using System.Text.Json;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal static class HttpJsonDocumentReader
{
    internal static async Task<Result<JsonDocument>> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken token)
    {
        using HttpResponseMessage response = await client.SendAsync(request, token);

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
