namespace Suruga.Primitives;

/// <summary>
/// Represents a read-only collection of <see cref="Track"/> instances.
/// </summary>
/// <param name="tracks">The tracks contained in the set.</param>
public sealed class TrackSet(IReadOnlyList<Track> tracks)
{
    /// <summary>
    /// Gets an empty <see cref="TrackSet"/> instance.
    /// </summary>
    public static TrackSet Empty { get; } = new([]);

    /// <summary>
    /// Gets a value indicating whether the set contains no tracks.
    /// </summary>
    public bool IsEmpty => Tracks.Count == 0;

    /// <summary>
    /// Gets the number of tracks in the set.
    /// </summary>
    public int Count => Tracks.Count;

    /// <summary>
    /// Gets the tracks contained in the set.
    /// </summary>
    public IReadOnlyList<Track> Tracks { get; } = tracks;
}
