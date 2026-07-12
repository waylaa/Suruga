using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Suruga.Options;

namespace Suruga.Persistence;

/// <summary>
/// Provides access to MongoDB databases and manages the lifetime of the underlying client.
/// </summary>
internal sealed class DatabaseClient : IDisposable
{
	private readonly MongoClient? _client;
	private readonly DatabaseOptions _options;
	
	private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseClient"/> class.
    /// </summary>
    /// <param name="options">The database configuration options.</param>
    public DatabaseClient(IOptions<DatabaseOptions> options)
	{
		_options = options.Value;
		
		if (_options.Enable)
		{
			_client = new MongoClient(MongoClientSettings.FromConnectionString(_options.ConnectionString));
		}
	}

    /// <summary>
    /// Gets a MongoDB database with the specified name.
    /// </summary>
    /// <param name="name">The name of the database.</param>
    /// <returns>
    /// The requested database if database support is enabled; otherwise, <see langword="null"/>.
    /// </returns>
    internal IMongoDatabase? GetDatabase(string name)
		=> _options.Enable ? _client?.GetDatabase(name) : null;

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		_client?.Dispose();
	}
}
