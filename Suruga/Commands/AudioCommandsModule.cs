using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Commands.Autocomplete;
using Suruga.Extensions;
using Suruga.Helpers;
using Suruga.Pagination;
using Suruga.Primitives;
using System.Globalization;

namespace Suruga.Commands;

internal sealed class AudioCommandsModule(AudioSessionManager sessionManager, PaginatorManager paginatorManager) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Attempts to play a track or URL.", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync([SlashCommandParameter(AutocompleteProviderType = typeof(TrackResultsAutocompleteProvider))] string query)
    {
        Guild guild = Context.Guild!;
        
        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out VoiceState? userVoiceState) || userVoiceState.ChannelId is not ulong voiceChannelId)
        {
            await RespondAsync(InteractionCallback.Message("You must be connected to a voice channel."));
            return;
        }
        
        if (string.IsNullOrWhiteSpace(query))
        {
            await RespondAsync(InteractionCallback.Message("Provide a URL or file path."));
            return;
        }
        
        AudioSession session = sessionManager.GetOrCreateSession(Context.Client, guild.Id, Context.Channel.Id);
        
        if (Context.Channel.Id != session.TextChannelId)
        {
            string name = guild.Channels[session.TextChannelId].Name;
            await RespondAsync(InteractionCallback.Message($"Use music commands in #{name}."));
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        
        AudioPlayer player = session.Player;

        await player.Engine.Connection.ConnectAsync(voiceChannelId);
        int tracksQueued = await player.PlayAsync(query, (GuildUser)Context.User);

        if (tracksQueued == 0)
        {
            await FollowupAsync($"Could not play: {query}");
            return;
        }
        
        if (player.State is AudioPlaybackState.Playing or AudioPlaybackState.Paused && player.Queue.HasNext)
        {
            // Re-create player message if it does not exist but a current track is playing.
            // This can happen because the player message is not persistently stored.
            if (!session.PlayerMessage.HasMessage && player.Queue.CurrentTrack is Track track)
            {
                RestMessage message = await CreatePlayerMessageAsync(Context.Interaction, track, player.State is AudioPlaybackState.Paused);
                session.PlayerMessage.Set(message);
                
                return;
            }
            
            // Else, reply with a 'Queued N tracks' message if the player is playing/paused and a queue exists.
            await FollowupAsync(tracksQueued > 1 ? $"Queued {tracksQueued} tracks." : "Queued 1 track.");
            return;
        }

        // If the player is playing/idle and there is only a single current track that may
        // be playing, then re-create the player message as there is no queue to depend
        // on a long-running player message.
        if (player.State is AudioPlaybackState.Playing or AudioPlaybackState.Idle &&
            player is { Queue: { CurrentTrack: Track nonQueuedTrack, HasCurrent: true, HasNext: false } })
        {
            await session.PlayerMessage.InvalidateAsync();
            
            RestMessage message = await CreatePlayerMessageAsync(Context.Interaction, nonQueuedTrack, player.State is AudioPlaybackState.Paused);
            session.PlayerMessage.Set(message);
        }
    }

    [SlashCommand("leave", "Leave voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task LeaveAsync()
    {
        Guild guild = Context.Guild!;
        
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        VoiceState voiceState = guild.VoiceStates[Context.Client.Id];
        string voiceChannelName = guild.Channels[voiceState.ChannelId.GetValueOrDefault()].Name;

        await session.Player.Engine.Connection.DisconnectAsync();
        await sessionManager.TryRemoveSessionAsync(guild.Id);
        await RespondAsync(InteractionCallback.Message($"Left {voiceChannelName}."));
    }

    [SlashCommand("stop", "Stops current track.", Contexts = [InteractionContextType.Guild])]
    public async Task StopAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        if (session.Player.State is AudioPlaybackState.Playing)
        {
            await session.Player.StopAsync();
            await RespondAsync(InteractionCallback.Message("Playback stopped."));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to stop."));
        }
    }

    [SlashCommand("pause", "Pauses the current track.", Contexts = [InteractionContextType.Guild])]
    public async Task PauseAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        if (session.Player.State is AudioPlaybackState.Playing)
        {
            await session.Player.PauseAsync();
            await RespondAsync(InteractionCallback.Message("Playback paused."));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to pause."));
        }
    }

    [SlashCommand("resume", "Resumes the current track.", Contexts = [InteractionContextType.Guild])]
    public async Task ResumeAsync()
    {
        Guild guild = Context.Guild!;

        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out VoiceState? userVoiceState))
        {
            await RespondAsync(InteractionCallback.Message("You must be connected to a voice channel."));
            return;
        }

        AudioSession session = sessionManager.GetOrCreateSession(Context.Client, guild.Id, Context.Channel.Id);
        AudioPlayer player = session.Player;

        if (player.State is AudioPlaybackState.Paused)
        {
            await player.ResumeAsync();
            await RespondAsync(InteractionCallback.Message("Playback resumed."));

            return;
        }

        // If the player is idle and there's an existing queue/current track, start playback.
        if (player.State is AudioPlaybackState.Idle && player.Queue is { HasCurrent: true, HasNext: true})
        {
            await player.Engine.Connection.ConnectAsync(userVoiceState.ChannelId!.Value);
            bool hasStarted = await player.ResumeFromStoredQueueAsync();

            if (hasStarted)
            {
                await RespondAsync(InteractionCallback.Message("Resumed playback from the queue."));
                return;
            }
        }

        await RespondAsync(InteractionCallback.Message("There is nothing to resume."));
    }
    
    [SlashCommand("skip", "Skips current track.", Contexts = [InteractionContextType.Guild])]
    public async Task SkipAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        AudioPlayer player = session.Player;
        
        if (player.State is AudioPlaybackState.Playing or AudioPlaybackState.Paused)
        {
            await session.Player.SkipAsync();
            await RespondAsync(InteractionCallback.Message("Skipped current track."));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to skip."));
        }
    }

    [SlashCommand("rewind", "Rewinds to the previous track.", Contexts = [InteractionContextType.Guild])]
    public async Task RewindAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        AudioPlayer player = session.Player;

        if (player.State is AudioPlaybackState.Playing or AudioPlaybackState.Paused && player.Queue.HasPrevious)
        {
            await session.Player.RewindAsync();
            await RespondAsync(InteractionCallback.Message("Rewound to previous track."));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is no previous track to rewind to."));
        }
    }

    [SlashCommand("seek", "Seeks the current track.", Contexts = [InteractionContextType.Guild])]
    public async Task SeekAsync(string timestamp)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        if (!TimeSpan.TryParse(timestamp, out TimeSpan time))
        {
            await RespondAsync(InteractionCallback.Message("Timestamp must be in HH:MM:SS format."));
            return;
        }

        if (session.Player.State is AudioPlaybackState.Playing or AudioPlaybackState.Paused)
        {
            session.Player.Seek(time);
            await RespondAsync(InteractionCallback.Message($"Seeking to {timestamp}"));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to seek."));
        }
    }

    [SlashCommand("loop", "Loops current track.", Contexts = [InteractionContextType.Guild])]
    public async Task LoopAsync(LoopMode mode = default)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        await session.Player.LoopAsync(mode);
        await RespondAsync(InteractionCallback.Message($"Loop mode set to {session.Player.Queue.LoopMode.GetDescription()}."));
    }

    [SlashCommand("queue", "List queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task QueueAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        IReadOnlyList<Track> queue = session.Player.Queue.Upcoming;
        PaginatorSession<Track> paginatorSession = new(new Paginator<Track>(queue, 10));

        EmbedProperties embed = EmbedHelper.Queue(session.Player.Queue.CurrentTrack, (GuildUser)Context.User, paginatorSession.Paginator);
        
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .AddEmbeds(embed)
            .AddComponents(ComponentsHelper.CreateQueuePaginationButtonsComponent(paginatorSession.Paginator))));
        
        RestMessage message = await GetResponseAsync();
        
        paginatorManager.Add(Context.Guild!.Id, message, paginatorSession, async msg => await msg
            .ModifyAsync(options => options.WithComponents([])));
    }
    
    [SlashCommand("history", "List all played and queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task HistoryAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        IReadOnlyList<Track> history = session.Player.Queue.History;
        PaginatorSession<Track> paginatorSession = new(new Paginator<Track>(history, 10));
        EmbedProperties embed = EmbedHelper.History((GuildUser)Context.User, paginatorSession.Paginator);
        
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .AddEmbeds(embed)
            .AddComponents(ComponentsHelper.CreateHistoryPaginationButtonsComponent(paginatorSession.Paginator))));
        
        RestMessage message = await GetResponseAsync();
        
        paginatorManager.Add(Context.Guild!.Id, message, paginatorSession, async msg => await msg
            .ModifyAsync(options => options.WithComponents([])));
    }

    [SlashCommand("shuffle", "Shuffles all queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task ShuffleAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        if (session.Player.State is AudioPlaybackState.Playing && session.Player.Queue.HasNext)
        {
            await session.Player.ShuffleAsync();
            await RespondAsync(InteractionCallback.Message("Shuffled the queue."));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to shuffle."));
        }
    }

    [SlashCommand("clear", "Clears the queue.", Contexts = [InteractionContextType.Guild])]
    public async Task ClearAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        await session.Player.ClearAsync();
        await RespondAsync(InteractionCallback.Message("Queue cleared."));
    }

    [SlashCommand("nowplaying", "Gets the currently playing track.", Contexts = [InteractionContextType.Guild])]
    public async Task NowPlayingAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        if (session.Player.Queue.CurrentTrack is not Track currentTrack)
        {
            await RespondAsync(InteractionCallback.Message("Nothing is currently playing."));
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());

        if (session.PlayerMessage.HasMessage)
        {
            await session.PlayerMessage.InvalidateAsync();
        }
        
        RestMessage message = await CreatePlayerMessageAsync(Context.Interaction, currentTrack, session.Player.State is AudioPlaybackState.Paused);
        session.PlayerMessage.Set(message);
    }
    
    [SlashCommand("volume", "Set playback volume.", Contexts = [InteractionContextType.Guild])]
    public async Task VolumeAsync([SlashCommandParameter(MinValue = 0, MaxValue = 100)] int value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        session.Player.Engine.PostProcessor.SetGain(value / 100f);
        await RespondAsync(InteractionCallback.Message($"Volume set to {value}%"));
    }

    [SlashCommand("speed", "Set playback speed.", Contexts = [InteractionContextType.Guild])]
    public async Task SpeedAsync([SlashCommandParameter(MinValue = 0.25f, MaxValue = 2f)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        session.Player.Engine.PostProcessor.SetTempo(value);
        await RespondAsync(InteractionCallback.Message($"Speed set to {value.ToString(CultureInfo.InvariantCulture)}x"));
    }

    [SlashCommand("pitch", "Set playback pitch.", Contexts = [InteractionContextType.Guild])]
    public async Task PitchAsync([SlashCommandParameter(MinValue = 0.25f, MaxValue = 2f)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        session.Player.Engine.PostProcessor.SetPitch(value);
        await RespondAsync(InteractionCallback.Message($"Pitch set to {value.ToString(CultureInfo.InvariantCulture)}x"));
    }

    [SlashCommand("rate", "Set playback rate.", Contexts = [InteractionContextType.Guild])]
    public async Task RateAsync([SlashCommandParameter(MinValue = 0.25f, MaxValue = 2f)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        session.Player.Engine.PostProcessor.SetRate(value);
        await RespondAsync(InteractionCallback.Message($"Rate set to {value.ToString(CultureInfo.InvariantCulture)}x"));
    }
    
    private async Task<AudioSession?> GetAudioSessionAsync()
    {
        Guild guild = Context.Guild!;

        if (!guild.VoiceStates.ContainsKey(Context.User.Id))
        {
            await RespondAsync(InteractionCallback.Message("You must be connected to a voice channel."));
            return null;
        }

        if (!guild.VoiceStates.ContainsKey(Context.Client.Id))
        {
            await RespondAsync(InteractionCallback.Message("I must be connected to a voice channel."));
            return null;
        }

        if (sessionManager.TryGetSession(guild.Id, out AudioSession? session))
        {
            if (Context.Channel.Id == session.TextChannelId)
            {
                return session;
            }
            
            string name = guild.Channels[session.TextChannelId].Name;
            await RespondAsync(InteractionCallback.Message($"Use commands in #{name}."));
                
            return null;
        }
        
        await RespondAsync(InteractionCallback.Message("Nothing is playing."));
        return null;
    }
    
    private static Task<RestMessage> CreatePlayerMessageAsync(Interaction interaction, Track track, bool isPaused)
    {
        return interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
            .WithEmbeds([EmbedHelper.NowPlaying(track)])
            .WithComponents([ComponentsHelper.CreatePlayerControlsComponent(isPaused)]));
    }
}
