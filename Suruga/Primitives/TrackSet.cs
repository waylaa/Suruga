namespace Suruga.Primitives;

/// <summary>
/// Represents a read-only collection of <see cref="Track"/> instances.
/// </summary>
/// <param name="tracks">The tracks contained in the set.</param>
internal sealed class TrackSet(IReadOnlyList<Track> tracks)
{
    /// <summary>
    /// Gets an empty <see cref="TrackSet"/> instance.
    /// </summary>
    internal static TrackSet Empty { get; } = new([]);

    /// <summary>
    /// Gets a value indicating whether the set contains no tracks.
    /// </summary>
    internal bool IsEmpty => Tracks.Count == 0;

    /// <summary>
    /// Gets the number of tracks in the set.
    /// </summary>
    internal int Count => Tracks.Count;

    /// <summary>
    /// Gets the tracks contained in the set.
    /// </summary>
    internal IReadOnlyList<Track> Tracks { get; } = tracks;
}
