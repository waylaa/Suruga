using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Suruga.Options;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class InvidiousCompanionClient(HttpClient client, IOptions<InvidiousCompanionOptions> optionsAccessor)
{
    private readonly InvidiousCompanionOptions _options = optionsAccessor.Value;
    
    internal async Task<Result<JsonDocument>> GetPlayerAsync(string videoId, CancellationToken token)
    {
        string protocol = _options.UseHttps ? "https" : "http";
        string url = $"{protocol}://{_options.Host}:{_options.Port}/companion/youtubei/v1/player";

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        request.Content = new StringContent(new JsonObject { ["videoId"] = videoId }.ToJsonString(), Encoding.UTF8, "application/json");

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
