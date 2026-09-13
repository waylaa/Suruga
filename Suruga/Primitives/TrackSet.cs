using System.Collections.ObjectModel;

namespace Suruga.Primitives;

internal sealed class TrackSet(IReadOnlyList<Track> tracks)
{
    internal static TrackSet Empty { get; } = new(ReadOnlyCollection<Track>.Empty);
    
    internal bool IsEmpty => Tracks.Count == 0;
    
    internal int Count => Tracks.Count;
    
    internal IReadOnlyList<Track> Tracks { get; } = tracks;
}
