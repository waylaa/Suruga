using MongoDB.Bson.Serialization.Attributes;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Persistence;

/// <summary>
/// Represents the persisted state of a guild's track queue.
/// </summary>
public sealed record TrackQueueState
{
    /// <summary>
    /// Gets the unique identifier of the guild that owns the queue.
    /// </summary>
    [BsonId]
	public required ulong GuildId { get; init; }

    /// <summary>
    /// Gets the tracks that have already been played.
    /// </summary>
    public required List<Track> History { get; init; }

    /// <summary>
    /// Gets the tracks scheduled to be played next.
    /// </summary>
    public required List<Track> Upcoming { get; init; }

    /// <summary>
    /// Gets the track that is currently playing.
    /// </summary>
    public required Track? CurrentTrack { get; init; }

    /// <summary>
    /// Gets the loop mode applied to the queue.
    /// </summary>
    public required LoopMode LoopMode { get; init; }
}
