namespace Suruga.Primitives;

/// <summary>
/// Represents a YouTube-based stream source that provides a collection of adaptive audio formats.
/// </summary>
/// <param name="Formats">
/// A read-only list of adaptive formats, typically ordered by quality or bitrate.
/// </param>
internal sealed record YoutubeStreamSource(IReadOnlyList<AdaptiveFormat> Formats) : StreamSource;
