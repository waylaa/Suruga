using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Audio;
using Suruga.Primitives;

namespace Suruga.Commands;

internal sealed class AudioCommands(AudioSessionManager sessionManager) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Attempts to play a track or URL.", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync
    (
        [SlashCommandParameter
        (
            Description = "A URL, file path or search term."
            /*, AutocompleteProviderType = typeof(TrackResultsAutocompleteProvider)*/
        )]
        string query
    )
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Loading));

        Guild guild = Context.Guild!;
        GatewayClient client = Context.Client;

        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out VoiceState? userVoiceState))
        {
            await ModifyResponseAsync(msg => msg.WithContent("Connect to a voice channel first."));
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            await ModifyResponseAsync(msg => msg.WithContent("Provide a URL or file path."));
            return;
        }

        AudioSession session = sessionManager.GetOrCreate(client, guild.Id, userVoiceState.ChannelId.GetValueOrDefault());
        await session.ConnectAsync(guild.Id, userVoiceState.ChannelId.GetValueOrDefault());
        
        AudioPlayer player = session.Player;
        
        Result<IReadOnlyList<AudioTrack>> enqueueResult = await player.EnqueueAsync(query);

        if (!enqueueResult.TryGetValue(out IReadOnlyList<AudioTrack>? tracks))
        {
            await ModifyResponseAsync(message => message.WithContent($"Failed to resolve '{query}'. ({enqueueResult.Error.Message})"));
            return;
        }

        switch (tracks.Count)
        {
            case 1:
                await ModifyResponseAsync(message => message.WithContent($"Now Playing: {FormatTrackMessage(tracks[0])}"));
                break;
            
            case > 1:
                await ModifyResponseAsync(message => message.WithContent($"Enqueued {tracks.Count} tracks."));
                break;
        }
    }

    [SlashCommand("leave", "Leave voice channel.")]
    public async Task LeaveAsync()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Loading));

        Guild guild = Context.Guild!;
        VoiceState state = await guild.GetCurrentUserVoiceStateAsync();
        string channelName = guild.Channels[state.ChannelId.GetValueOrDefault()].Name;
        
        await sessionManager.RemoveAsync(guild.Id);
        await ModifyResponseAsync(message => message.WithContent($"Left {channelName}."));
    }

    [SlashCommand("stop", "Stops current track.")]
    public async Task StopAsync()
    {
        
    }

    [SlashCommand("skip", "Skips current track.")]
    public async Task SkipAsync()
    {
        
    }

    [SlashCommand("resume", "Resumes current track.")]
    public async Task ResumeAsync()
    {
        
    }

    [SlashCommand("loop", "Loops current track.")]
    public async Task LoopAsync()
    {
    }

    [SlashCommand("list", "List queued tracks.")]
    public async Task ListAsync()
    {
        
    }
    
    [SlashCommand("speed", "Set playback speed.", Contexts = [InteractionContextType.Guild])]
    public async Task SpeedAsync([SlashCommandParameter(MinValue = 0.25, MaxValue = 2)] float speed)
    {
        AudioSession session = await RetrieveSessionAsync();
        session.Player.SetSpeed(speed);
        
        await RespondAsync(InteractionCallback.Message($"Speed set to {Math.Round(speed * 100)}%"));
    }

    [SlashCommand("volume", "Set playback volume.", Contexts = [InteractionContextType.Guild])]
    public async Task VolumeAsync([SlashCommandParameter(MinValue = 0, MaxValue = 100)]int volume)
    {
        AudioSession session = await RetrieveSessionAsync();
        session.Player.SetVolume(volume);
        
        await RespondAsync(InteractionCallback.Message($"Volume set to {volume}%"));
    }

    [SlashCommand("pitch", "Set playback pitch.", Contexts = [InteractionContextType.Guild])]
    public async Task PitchAsync([SlashCommandParameter(MinValue = 0.25, MaxValue = 2)] float pitch)
    {
        AudioSession session = await RetrieveSessionAsync();
        session.Player.SetPitch(pitch);
        
        await RespondAsync(InteractionCallback.Message($"Pitch set to {Math.Round(pitch * 100)}%"));
    }

    private async Task<AudioSession> RetrieveSessionAsync()
    {
        Guild guild = Context.Guild!;
        VoiceState state = await guild.GetCurrentUserVoiceStateAsync();
        
        return sessionManager.GetOrCreate(Context.Client, guild.Id, state.ChannelId.GetValueOrDefault());
    }

    private static string FormatTrackMessage(AudioTrack track)
        => $"[{track.Title}]({track.Url}).";
}
