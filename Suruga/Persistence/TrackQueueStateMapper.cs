using System.Text.Json;
using Microsoft.Data.Sqlite;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Persistence;

internal static class TrackQueueStateMapper
{
    internal const string SelectColumns = "CurrentIndex, LoopMode, TracksJson";

    internal static TrackQueueState Read(SqliteDataReader reader, ulong guildId)
    {
        int currentIndex = reader.GetInt32(0);
        LoopMode loopMode = Enum.Parse<LoopMode>(reader.GetString(1));
        List<Track> tracks = JsonSerializer.Deserialize(reader.GetString(2), TrackJsonContext.Default.ListTrack) ?? [];

        return new TrackQueueState
        {
            GuildId = guildId,
            Tracks = tracks,
            CurrentIndex = currentIndex,
            LoopMode = loopMode
        };
    }

    internal static void BindParameters(SqliteCommand command, TrackQueueState state)
    {
        string tracksJson = JsonSerializer.Serialize(state.Tracks, TrackJsonContext.Default.ListTrack);

        command.Parameters.AddWithValue("$guildId", (long)state.GuildId);
        command.Parameters.AddWithValue("$currentIndex", state.CurrentIndex);
        command.Parameters.AddWithValue("$loopMode", state.LoopMode.ToString());
        command.Parameters.AddWithValue("$tracksJson", tracksJson);
    }
}
