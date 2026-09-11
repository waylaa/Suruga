using Microsoft.Data.Sqlite;

namespace Suruga.Persistence;

internal sealed class TrackQueueStateRepository(DatabaseClient client, PersistenceAvailability availability)
{
    private readonly SqliteOperationExecutor _executor = new(client);

    internal TrackQueueState? Load(ulong guildId)
    {
        if (!availability.IsEnabled)
        {
            return null;
        }

        return _executor.Execute(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {TrackQueueStateMapper.SelectColumns} FROM QueueStates WHERE GuildId = $guildId;";
            command.Parameters.AddWithValue("$guildId", (long)guildId);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? TrackQueueStateMapper.Read(reader, guildId) : null;
        });
    }

    internal Task SaveAsync(TrackQueueState state, CancellationToken token = default)
    {
        if (!availability.IsEnabled)
        {
            return Task.CompletedTask;
        }

        return _executor.ExecuteAsync(async connection =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO QueueStates (GuildId, CurrentIndex, LoopMode, TracksJson)
                VALUES ($guildId, $currentIndex, $loopMode, $tracksJson)
                ON CONFLICT(GuildId) DO UPDATE SET
                    CurrentIndex = excluded.CurrentIndex,
                    LoopMode = excluded.LoopMode,
                    TracksJson = excluded.TracksJson;
                """;

            TrackQueueStateMapper.BindParameters(command, state);
            await command.ExecuteNonQueryAsync(token);
        }, token);
    }

    internal Task RemoveAsync(ulong guildId, CancellationToken token = default)
    {
        if (!availability.IsEnabled)
        {
            return Task.CompletedTask;
        }

        return _executor.ExecuteAsync(async connection =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM QueueStates WHERE GuildId = $guildId;";
            command.Parameters.AddWithValue("$guildId", (long)guildId);
            
            await command.ExecuteNonQueryAsync(token);
        }, token);
    }
}
