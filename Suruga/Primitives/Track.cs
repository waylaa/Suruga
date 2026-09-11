namespace Suruga.Primitives;

/// <summary>
/// Represents an audio track with its associated metadata and playback information.
/// </summary>
internal sealed class Track
{
	/// <summary>
	/// Gets the platform where the track originates from.
	/// </summary>
	internal TrackPlatform Platform { get; }

    /// <summary>
    /// Gets the unique identifier of the track on its source platform.
    /// </summary>
    internal string Id { get; }

    /// <summary>
    /// Gets the URI or file path used to locate and stream the track.
    /// </summary>
    internal string Uri { get; }

    /// <summary>
    /// Gets the title of the track.
    /// </summary>
    internal string? Title { get; init; }

    /// <summary>
    /// Gets the author, artist, or uploader of the track.
    /// </summary>
    internal string? Author { get; init; }

    /// <summary>
    /// Gets the URI of the track's thumbnail or cover art.
    /// </summary>
    internal string? ThumbnailUri { get; init; }

    /// <summary>
    /// Gets the total duration of the track.
    /// </summary>
    internal TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets the starting position for playback within the track.
    /// </summary>
    internal TimeSpan? StartPosition { get; init; }

    /// <summary>
    /// Gets the context or user who requested the track.
    /// </summary>
    internal TrackRequestContext? RequestedBy { get; init; }

    internal Track(TrackPlatform platform, string id, string uri)
    {
	    Platform = platform;
	    Id = id;
	    Uri = uri;
    }

    /// <summary>
    /// Returns a string representation of the track.
    /// </summary>
    /// <returns>
    /// For local tracks, returns the <see cref="Title"/> or <see cref="Id"/>. 
    /// For remote tracks, returns a Markdown formatted link containing the <see cref="Title"/> and <see cref="Uri"/>.
    /// </returns>
    public override string ToString()
		=> Platform is TrackPlatform.Local ? Title ?? Id : $"[{Title}]({Uri})";
}
