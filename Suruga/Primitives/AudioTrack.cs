namespace Suruga.Primitives;

public sealed record AudioTrack
{
    public required AudioPlatform Platform { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public required string StreamUrl { get; init; }

    public string? Author { get; init; }

    public string? ThumbnailUrl { get; init; }

    public TimeSpan? Duration { get; init; }

    public Func<object>? Callback { get; init; }

    public IReadOnlyDictionary<string, object>? CallbackInfo { get; init; }
}
