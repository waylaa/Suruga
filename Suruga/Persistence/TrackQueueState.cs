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
    
    public required List<Track> Tracks { get; init; }

    /// <summary>
    /// Gets the track that is currently playing.
    /// </summary>
    public required int CurrentIndex { get; init; }

    /// <summary>
    /// Gets the loop mode applied to the queue.
    /// </summary>
    public required LoopMode LoopMode { get; init; }
}
