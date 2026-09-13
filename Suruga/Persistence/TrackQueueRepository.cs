using System.Text.Json;
using Microsoft.Data.Sqlite;
using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Common;
using Suruga.Primitives;

namespace Suruga.Persistence;

internal sealed class TrackQueueRepository(SqliteOperationExecutor executor)
{
    internal TrackQueue.TrackQueueSnapshot? Load(ulong guildId)
    {
        TrackQueue.TrackQueueSnapshot? snapshot = executor.Execute(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT CurrentIndex, LoopMode, TracksJson 
                FROM QueueStates 
                WHERE GuildId = $guildId
                """;
            
            command.Parameters.AddWithValue("$guildId", (long)guildId);
            
            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }
            
            int currentIndex = reader.GetInt32(0);
            string loopModeStr = reader.GetString(1);
            string tracksJson = reader.GetString(2);

            if (!Enum.TryParse(loopModeStr, out LoopMode loopMode))
            {
                loopMode = LoopMode.None;
            }

            List<Track>? tracks = JsonSerializer.Deserialize(tracksJson, TrackJsonContext.Default.ListTrack) ?? [];
            return new TrackQueue.TrackQueueSnapshot(tracks, currentIndex, loopMode);
        });

        if (snapshot is null)
        {
            Logger.Debug<TrackQueueRepository>($"No persisted queue state found for guild {guildId}");
            return null;
        }
        
        Logger.Trace<TrackQueueRepository>($"Loaded persisted queue state for guild {guildId}: {snapshot.Tracks.Count} track(s), current index {snapshot.CurrentTrackIndex}");
        return snapshot;
    }
    
    internal async Task SaveAsync(ulong guildId, TrackQueue.TrackQueueSnapshot snapshot, CancellationToken token = default)
    {
        string tracksJson = JsonSerializer.Serialize(snapshot.Tracks.ToList(), TrackJsonContext.Default.ListTrack);
        string loopModeStr = snapshot.LoopMode.ToString();

        await executor.ExecuteAsync(async connection =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO QueueStates (GuildId, CurrentIndex, LoopMode, TracksJson)
                VALUES ($guildId, $currentIndex, $loopMode, $tracksJson)
                ON CONFLICT(GuildId) DO UPDATE SET
                    CurrentIndex = excluded.CurrentIndex,
                    LoopMode = excluded.LoopMode,
                    TracksJson = excluded.TracksJson
                """;
            
            command.Parameters.AddWithValue("$guildId", (long)guildId);
            command.Parameters.AddWithValue("$currentIndex", snapshot.CurrentTrackIndex);
            command.Parameters.AddWithValue("$loopMode", loopModeStr);
            command.Parameters.AddWithValue("$tracksJson", tracksJson);
            
            await command.ExecuteNonQueryAsync(token);
        });
        
        Logger.Trace<TrackQueueRepository>($"Persisted queue state for guild {guildId}");
    }

    internal async Task RemoveAsync(ulong guildId, CancellationToken token = default)
    {
        await executor.ExecuteAsync(async connection =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM QueueStates WHERE GuildId = $guildId";
            command.Parameters.AddWithValue("$guildId", (long)guildId);

            await command.ExecuteNonQueryAsync(token);
        });
        
        Logger.Trace<TrackQueueRepository>($"Removed persisted queue state for guild {guildId}");
    }
}
