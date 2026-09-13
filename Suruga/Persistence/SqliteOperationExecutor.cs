using Microsoft.Data.Sqlite;
using Suruga.Common;

namespace Suruga.Persistence;

internal sealed class SqliteOperationExecutor(DatabaseClient client)
{
    internal T? Execute<T>(Func<SqliteConnection, T> operation) where T : class?
    {
        if (!client.TryGetConnection(out SqliteConnection? connection))
        {
            return null;
        }

        using (connection)
        {
            try
            {
                return operation(connection);
            }
            catch (SqliteException ex)
            {
                Logger.Error<SqliteOperationExecutor>(ex, "An asynchronous database operation failed.");
                return null;
            }
        }
    }

    internal async Task ExecuteAsync(Func<SqliteConnection, Task> operation)
    {
        if (!client.TryGetConnection(out SqliteConnection? connection))
        {
            return;
        }

        await using (connection)
        {
            try
            {
                await operation(connection);
            }
            catch (SqliteException ex)
            {
                Logger.Error<SqliteOperationExecutor>(ex, "An asynchronous database operation failed.");
            }
            catch (OperationCanceledException)
            {
                Logger.Error<SqliteOperationExecutor>("An asynchronous database operation was canceled.");
            }
        }
    }
}
