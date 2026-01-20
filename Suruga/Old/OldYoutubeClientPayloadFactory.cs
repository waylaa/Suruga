using System.Text;
using System.Text.Json.Nodes;

namespace Suruga.Resolvers.Youtube.Factories;

internal static class YoutubeClientPayloadFactory
{
    private const string ApiKey = "AIzaSyA8eiZmM1FaDVjRy-df2KTyQ_vz_yYM39w";
    
    internal static HttpRequestMessage CreateAndroidPlayerPayload(string videoId, string visitorData)
    {
        JsonObject body = new()
        {
            ["videoId"] = videoId,
            ["contentCheckOk"] = true,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "ANDROID",
                    ["clientVersion"] = "20.51.39",
                    ["osName"] = "Android",
                    ["osVersion"] = "13",
                    ["platform"] = "MOBILE",
                    ["visitorData"] = visitorData,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = 0
                }
            }
        };

        HttpRequestMessage request = new(HttpMethod.Post, $"/youtubei/v1/player?key={ApiKey}&hl=en");
        request.Headers.UserAgent.ParseAdd("com.google.android.youtube/20.51.39 (Linux; U; ANDROID 13) gzip");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        return request;
    }

    internal static HttpRequestMessage CreateTvPlayerPayload(string videoId, string visitorData, string signatureTimestamp)
    {
        JsonObject body = new()
        {
            ["videoId"] = videoId,
            ["context"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["clientName"] = "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
                    ["clientVersion"] = "2.0",
                    ["visitorData"] = visitorData,
                    ["hl"] = "en",
                    ["gl"] = "US",
                    ["utcOffsetMinutes"] = 0
                },
                ["thirdParty"] = new JsonObject
                {
                    ["embedUrl"] = "https://www.youtube.com"
                }
            },
            ["playbackContext"] = new JsonObject
            {
                ["contentPlaybackContext"] = new JsonObject
                {
                    ["signatureTimestamp"] = signatureTimestamp
                }
            }
        };

        HttpRequestMessage request = new(HttpMethod.Post, $"/youtubei/v1/player?key={ApiKey}&hl=en");
        request.Headers.Add("Origin", "https://www.youtube.com");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        return request;
    }
}
