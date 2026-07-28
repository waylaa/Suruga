using System.Diagnostics.CodeAnalysis;
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
	[MemberNotNullWhen(true, nameof(_collection))]
	private bool IsReady { get; }

	private static readonly ReplaceOptions ReplaceOptions = new() { IsUpsert = true };
	
	private readonly IMongoCollection<TrackQueueState>? _collection;
	private readonly DatabaseOptions _databaseOptions;
	private readonly InvidiousCompanionOptions _invidiousCompanionOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackQueueStateRepository"/> class.
    /// </summary>
    /// <param name="client">The database client used to access MongoDB.</param>
    /// <param name="databaseOptions">The database configuration options.</param>
    /// <param name="invidiousCompanionOptions"></param>
    public TrackQueueStateRepository
	(
		DatabaseClient client,
		IOptions<DatabaseOptions> databaseOptions,
		IOptions<InvidiousCompanionOptions> invidiousCompanionOptions
	)
	{
		_databaseOptions = databaseOptions.Value;
		_invidiousCompanionOptions = invidiousCompanionOptions.Value;
		
		ConventionRegistry.Register
		(
			"EnumStringConvention",
			new ConventionPack { new EnumRepresentationConvention(BsonType.String) },
			_ => true
		);
		
		IsReady = client.TryGetDatabase(_databaseOptions.Name, out IMongoDatabase? database);
		_collection = database?.GetCollection<TrackQueueState>("QueueStates");
	}

	/// <summary>
	/// Loads the persisted queue state for the specified guild.
	/// </summary>
	/// <param name="guildId">The identifier of the guild.</param>
	/// <param name="token"></param>
	/// <returns>
	/// The persisted queue state if one exists; otherwise, <see langword="null"/>.
	/// </returns>
	internal async Task<TrackQueueState?> Load(ulong guildId, CancellationToken token = default)
	{
		if (!_databaseOptions.Enable || !_invidiousCompanionOptions.Enable || !IsReady)
		{
			return null;
		}

		try
		{
			using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
			
			return await _collection
				.Find(x => x.GuildId == guildId)
				.FirstOrDefaultAsync(timeoutCts.Token);
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		catch (TimeoutException)
		{
			return null;
		}
		catch (MongoConnectionException)
		{
			return null;
		}
		catch (MongoServerException)
		{
			return null;
		}
	}
	
    /// <summary>
    /// Saves the specified queue state.
    /// </summary>
    /// <param name="state">The queue state to save.</param>
    /// <param name="token">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    internal async Task SaveAsync(TrackQueueState state, CancellationToken token = default)
	{
		if (!_databaseOptions.Enable || !_invidiousCompanionOptions.Enable || !IsReady)
		{
			return;
		}

		try
		{
			using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
			
			await _collection.ReplaceOneAsync(x => x.GuildId == state.GuildId, state, ReplaceOptions, timeoutCts.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (MongoException)
		{
		}
	}

    /// <summary>
    /// Removes the persisted queue state for the specified guild.
    /// </summary>
    /// <param name="guildId">The identifier of the guild.</param>
    /// <param name="token">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    internal async Task RemoveAsync(ulong guildId, CancellationToken token = default)
	{
		if (!_databaseOptions.Enable || !_invidiousCompanionOptions.Enable || !IsReady)
		{
			return;
		}
		
		try
		{
			using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

			await _collection.DeleteOneAsync(x => x.GuildId == guildId, timeoutCts.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (MongoException)
		{
		}
	}
}
