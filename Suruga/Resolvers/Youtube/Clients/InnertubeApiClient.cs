using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

internal sealed class InnertubeApiClient(HttpClient client)
{
    internal async Task<Result<JsonDocument>> PostAsync
    (
        string url,
        InnertubeClientProfile profile,
        string visitorData,
        JsonObject bodyWithoutContext,
        CancellationToken token
    )
    {
        bodyWithoutContext["context"] = BuildContext(profile, visitorData);
        using HttpRequestMessage request = new(HttpMethod.Post, url);

        ApplyHeaders(request, profile, visitorData);
        request.Content = new StringContent(bodyWithoutContext.ToJsonString(), Encoding.UTF8, "application/json");

        return await HttpJsonDocumentReader.SendAsync(client, request, token);
    }

    private static JsonObject BuildContext(InnertubeClientProfile profile, string visitorData)
    {
        JsonObject clientNode = new()
        {
            ["clientName"] = profile.ClientName,
            ["clientVersion"] = profile.ClientVersion,
            ["platform"] = profile.Platform,
            ["visitorData"] = visitorData,
            ["hl"] = "en",
            ["gl"] = "US",
            ["utcOffsetMinutes"] = (int)DateTimeOffset.Now.Offset.TotalMinutes
        };

        if (profile.OsName is not null)
        {
            clientNode["osName"] = profile.OsName;
        }

        if (profile.OsVersion is not null)
        {
            clientNode["osVersion"] = profile.OsVersion;
        }

        if (profile.AndroidSdkVersion is not null)
        {
            clientNode["androidSdkVersion"] = profile.AndroidSdkVersion;
        }

        if (profile.DeviceMake is not null)
        {
            clientNode["deviceMake"] = profile.DeviceMake;
        }

        if (profile.DeviceModel is not null)
        {
            clientNode["deviceModel"] = profile.DeviceModel;
        }

        return new JsonObject { ["client"] = clientNode };
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
