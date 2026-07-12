using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Suruga.Options;

namespace Suruga.Persistence;

/// <summary>
/// Provides persistence operations for <see cref="TrackQueueState"/> instances.
/// </summary>
internal sealed class TrackQueueStateRepository
{
	private readonly IMongoCollection<TrackQueueState>? _collection;
	private readonly DatabaseOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackQueueStateRepository"/> class.
    /// </summary>
    /// <param name="client">The database client used to access MongoDB.</param>
    /// <param name="options">The database configuration options.</param>
    public TrackQueueStateRepository(DatabaseClient client, IOptions<DatabaseOptions> options)
	{
		_options = options.Value;
		
		if (_options.Enable)
		{
			ConventionRegistry.Register
			(
				"EnumStringConvention",
				new ConventionPack { new EnumRepresentationConvention(BsonType.String) },
				_ => true
			);
			
			IMongoDatabase? database = client.GetDatabase(_options.Name);
			_collection = database?.GetCollection<TrackQueueState>("QueueStates");
		}
	}

    /// <summary>
    /// Loads the persisted queue state for the specified guild.
    /// </summary>
    /// <param name="guildId">The identifier of the guild.</param>
    /// <returns>
    /// The persisted queue state if one exists; otherwise, <see langword="null"/>.
    /// </returns>
    internal TrackQueueState? Load(ulong guildId)
		=> _options.Enable ? _collection?.Find(x => x.GuildId == guildId).FirstOrDefault() : null;

    /// <summary>
    /// Saves the specified queue state.
    /// </summary>
    /// <param name="state">The queue state to save.</param>
    /// <param name="token">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    internal Task SaveAsync(TrackQueueState state, CancellationToken token = default)
	{
		return _options.Enable && _collection is not null
			? _collection.ReplaceOneAsync(x => x.GuildId == state.GuildId, state, new ReplaceOptions { IsUpsert = true}, token)
			: Task.CompletedTask;
	}

    /// <summary>
    /// Removes the persisted queue state for the specified guild.
    /// </summary>
    /// <param name="guildId">The identifier of the guild.</param>
    /// <param name="token">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    internal Task RemoveAsync(ulong guildId, CancellationToken token = default)
	{
		return _options.Enable && _collection is not null
			? _collection.DeleteOneAsync(x => x.GuildId == guildId, cancellationToken: token)
			: Task.CompletedTask;
	}
}
