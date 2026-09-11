using Microsoft.Data.Sqlite;

namespace Suruga.Persistence;

internal sealed class SqliteOperationExecutor
{
    private readonly DatabaseClient _client;
    
    internal SqliteOperationExecutor(DatabaseClient client)
        => _client = client;
    
    internal T? Execute<T>(Func<SqliteConnection, T> operation) where T : class?
    {
        if (!_client.TryGetConnection(out SqliteConnection? connection))
        {
            return null;
        }

        using (connection)
        {
            try
            {
                return operation(connection);
            }
            catch (SqliteException)
            {
                return null;
            }
        }
    }

    internal async Task ExecuteAsync(Func<SqliteConnection, Task> operation, CancellationToken token)
    {
        if (!_client.TryGetConnection(out SqliteConnection? connection))
        {
            return;
        }

        await using (connection)
        {
            try
            {
                await operation(connection);
            }
            catch (SqliteException)
            {
                // Ignore.
            }
            catch (OperationCanceledException)
            {
                // Ignore.
            }
        }
    }
}
