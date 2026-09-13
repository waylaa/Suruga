using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Suruga.Common;
using Suruga.Options;

namespace Suruga.Persistence;

internal sealed class DatabaseClient(IOptions<DatabaseOptions> options) : IDisposable
{
    private readonly DatabaseOptions _options = options.Value;
    
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = string.IsNullOrWhiteSpace(options.Value.Path) ? Path.Combine(AppContext.BaseDirectory, "suruga.db") : options.Value.Path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();
    
    private readonly Lock _lock = new();
    
    private bool _isSchemaInitialized;
    private bool _isDisposed;

    internal bool TryGetConnection([NotNullWhen(true)] out SqliteConnection? connection)
    {
        connection = null;

        if (!_options.Enable)
        {
            Logger.Trace<DatabaseClient>("Connection requested but persistence is disabled via configuration.");
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
        catch (SqliteException ex)
        {
            Logger.Error<DatabaseClient>(ex, "Failed to open a database connection.");
            
            connection?.Dispose();
            connection = null;

            return false;
        }
    }

    private void EnsureSchema(SqliteConnection connection)
    {
        if (_isSchemaInitialized)
        {
            return;
        }

        using (_lock.EnterScope())
        {
            if (_isSchemaInitialized)
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
            _isSchemaInitialized = true;
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
