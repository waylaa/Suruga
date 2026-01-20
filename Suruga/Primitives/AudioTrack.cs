using Suruga.Transport.Primitives;

namespace Suruga.Primitives;

public sealed record AudioTrack
{
    public required AudioStreamDescriptorBase Stream { get; init; }

    public required AudioPlatform Platform { get; init; }

    public required string Title { get; init; }

    public string? Author { get; init; }

    public string? ThumbnailUrl { get; init; }

    public TimeSpan? Duration { get; init; }
}
