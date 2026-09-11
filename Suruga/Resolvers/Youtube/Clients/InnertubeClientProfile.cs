namespace Suruga.Resolvers.Youtube.Clients;

internal sealed record InnertubeClientProfile
{
    internal required string ClientName { get; init; }
    internal required string ClientVersion { get; init; }
    internal required string Platform { get; init; }
    internal required string UserAgent { get; init; }
    internal required string ClientNameHeaderValue { get; init; }

    internal string? OsName { get; init; }
    internal string? OsVersion { get; init; }
    internal int? AndroidSdkVersion { get; init; }
    internal string? DeviceMake { get; init; }
    internal string? DeviceModel { get; init; }
}
