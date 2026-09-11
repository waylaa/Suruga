using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Suruga.Options;

namespace Suruga.Persistence;

internal sealed class DatabaseClient : IDisposable
{
    private readonly DatabaseOptions _options;
    private readonly string _connectionString;
    private readonly Lock _lock = new();
    
    private bool _isInitialized;
    private bool _isDisposed;

    public DatabaseClient(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
        
        string path = string.IsNullOrWhiteSpace(_options.Path)
            ? Path.Combine(AppContext.BaseDirectory, "suruga.db")
            : _options.Path;
        
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    internal bool TryGetConnection([NotNullWhen(true)] out SqliteConnection? connection)
    {
        connection = null;

        if (!_options.Enable)
        {
            return false;
        }

        try
        {
            connection = new SqliteConnection(_connectionString);
            connection.Open();

            using (SqliteCommand pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 2000;";
                pragma.ExecuteNonQuery();
            }

            EnsureSchema(connection);
            return true;
        }
        catch (SqliteException)
        {
            connection?.Dispose();
            connection = null;

            return false;
        }
    }

    private void EnsureSchema(SqliteConnection connection)
    {
        if (_isInitialized)
        {
            return;
        }

        using (_lock.EnterScope())
        {
            if (_isInitialized)
            {
                return;
            }
            
            using SqliteCommand command = connection.CreateCommand();
            
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS QueueStates (
                    GuildId INTEGER PRIMARY KEY,
                    CurrentIndex INTEGER NOT NULL,
                    LoopMode TEXT NOT NULL,
                    TracksJson TEXT NOT NULL
                )
                """;
            
            command.ExecuteNonQuery();
            _isInitialized = true;
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        SqliteConnection.ClearAllPools();
    }
}
