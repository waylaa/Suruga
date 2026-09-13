using System.Text.Json.Serialization;

namespace Suruga.Primitives;

internal sealed class Track(TrackPlatform platform, string id, string uri)
{
	[JsonInclude]
	internal TrackPlatform Platform { get; } = platform;

	[JsonInclude]
	internal string Id { get; } = id;

	[JsonInclude]
	internal string Uri { get; } = uri;
    
	[JsonInclude]
    internal string? Title { get; init; }
    
	[JsonInclude]
    internal string? Author { get; init; }
    
	[JsonInclude]
    internal string? ThumbnailUri { get; init; }
    
	[JsonInclude]
    internal TimeSpan? Duration { get; init; }
    
	[JsonInclude]
    internal TimeSpan? StartPosition { get; init; }
    
	[JsonInclude]
    internal TrackRequestContext? RequestedBy { get; init; }
    
    public override string ToString()
		=> Platform is TrackPlatform.Local ? Title ?? Id : $"[{Title}]({Uri})";
}
