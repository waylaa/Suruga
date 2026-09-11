using System.Text.Json.Nodes;

namespace Suruga.Resolvers.Youtube.Clients;

internal static class InnertubeContextBuilder
{
    internal static JsonObject Build(InnertubeClientProfile profile, string visitorData)
    {
        JsonObject client = new()
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
            client["osName"] = profile.OsName;
        }
        
        if (profile.OsVersion is not null)
        {
            client["osVersion"] = profile.OsVersion;
        }
        
        if (profile.AndroidSdkVersion is not null)
        {
            client["androidSdkVersion"] = profile.AndroidSdkVersion;
        }
        
        if (profile.DeviceMake is not null)
        {
            client["deviceMake"] = profile.DeviceMake;
        }
        
        if (profile.DeviceModel is not null)
        {
            client["deviceModel"] = profile.DeviceModel;
        }

        return new JsonObject { ["client"] = client };
    }
}
