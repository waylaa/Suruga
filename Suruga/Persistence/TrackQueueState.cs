using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Persistence;

/// <summary>
/// Represents the persisted state of a guild's track queue.
/// </summary>
internal sealed record TrackQueueState
{
    /// <summary>
    /// Gets the unique identifier of the guild that owns the queue.
    /// </summary>
    internal required ulong GuildId { get; init; }
    
    internal required List<Track> Tracks { get; init; }

    /// <summary>
    /// Gets the track that is currently playing.
    /// </summary>
    internal required int CurrentIndex { get; init; }

    /// <summary>
    /// Gets the loop mode applied to the queue.
    /// </summary>
    internal required LoopMode LoopMode { get; init; }
}
