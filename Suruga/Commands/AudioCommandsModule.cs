using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Commands.Autocomplete;
using Suruga.Helpers;
using Suruga.Pagination;
using Suruga.Primitives;
using System.Globalization;
using Suruga.Audio.Commands.Connection;
using Suruga.Audio.Commands.Playback;
using Suruga.Resolvers;

namespace Suruga.Commands;

internal sealed class AudioCommandsModule
(
    TrackResolverRouter router,
    AudioSessionManager sessionManager,
    PaginatorManager paginatorManager
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Attempts to play a track or URL.", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync([SlashCommandParameter(AutocompleteProviderType = typeof(TrackResultsAutocompleteProvider))] string query)
    {
        Guild guild = Context.Guild!;
        
        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out VoiceState? userVoiceState) ||
            userVoiceState.ChannelId is not ulong voiceChannelId)
        {
            await RespondAsync(InteractionCallback.Message("You must be connected to a voice channel."));
            return;
        }
        
        if (string.IsNullOrWhiteSpace(query))
        {
            await RespondAsync(InteractionCallback.Message("Provide a URL or file path."));
            return;
        }
        
        AudioSession session = sessionManager.GetOrCreateSession(guild.Id, Context.Channel.Id);
        ulong? boundChannelId = session.PlayerMessage.BoundChannelId;
        
        if (Context.Channel.Id != boundChannelId && boundChannelId is ulong channelId)
        {
            string name = guild.Channels[channelId].Name;
            await RespondAsync(InteractionCallback.Message($"Use music commands in #{name}."));
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        
        TrackRequestContext requestContext = TrackRequestContext.FromUser((GuildUser)Context.User);
        Result<TrackSet> resolveResult = await router.ResolveAsync(query, requestContext);

        if (!resolveResult.TryGetValue(out TrackSet? set))
        {
            await FollowupAsync($"Could not play: {query}");
            return;
        }

        if (set.IsEmpty)
        {
            await FollowupAsync($"Could not find any tracks for: {query}");
            return;
        }

        AudioConnection connection = session.Connection;
        AudioPlayer player = session.Player;

        CommandResult connectionResult = await connection.PostAsync(new ConnectCommand(voiceChannelId));

        if (connectionResult.Status is CommandStatus.InvalidVoice)
        {
            await FollowupAsync("Failed to connect to voice channel.");
            return;
        }

        bool wasNotIdle = player.State is not AudioPlayerState.Idle;
        
        if (!session.PlayerMessage.HasMessage || (session.PlayerMessage.HasMessage && player.State is AudioPlayerState.Idle))
        {
            await session.PlayerMessage.SetAsync(Context.Interaction, player.State);
        }
        
        CommandResult playerResult = await player.PostAsync(new PlayAudioCommand(set));

        if (playerResult.Status is CommandStatus.NoTracks)
        {
            await FollowupAsync($"Could not find any tracks for: {query}");
            return;
        }

        if (session.PlayerMessage.HasMessage && player.Queue.HasNext && wasNotIdle)
        {
            await FollowupAsync(set.Count > 1 ? $"Queued {set.Count} tracks." : "Queued 1 track.");
        }
    }

    [SlashCommand("leave", "Leave voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task LeaveAsync()
    {
        Guild guild = Context.Guild!;
        
        if (await GetAudioSessionAsync() is null)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());

        VoiceState voiceState = guild.VoiceStates[Context.Client.Id];
        string voiceChannelName = guild.Channels[voiceState.ChannelId.GetValueOrDefault()].Name;
        
        if (!sessionManager.TryRemoveSession(guild.Id, out AudioSession? session))
        {
            await FollowupAsync($"Audio session failure in {voiceChannelName}.");
            return;
        }
        
        AudioConnection connection = session.Connection;
        await connection.PostAsync(new DisconnectCommand());

        AudioPlayer player = session.Player;
        await player.PostAsync(new StopAudioCommand());
        
        await session.DisposeAsync();
        await FollowupAsync($"Left {voiceChannelName}.");
    }

    [SlashCommand("stop", "Stops current track.", Contexts = [InteractionContextType.Guild])]
    public async Task StopAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new StopAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await FollowupAsync("Playback stopped.");
                break;
            
            case CommandStatus.AlreadyStopped:
                await FollowupAsync("There is nothing to stop.");
                break;
        }
    }

    [SlashCommand("pause", "Pauses the current track.", Contexts = [InteractionContextType.Guild])]
    public async Task PauseAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new PauseAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await FollowupAsync("Playback paused.");
                break;
            
            case CommandStatus.AlreadyPaused:
                await FollowupAsync("I am already paused.");
                break;
            
            case CommandStatus.NothingToPause:
                await FollowupAsync("There is nothing to pause.");
                break;
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
        
        await RespondAsync(InteractionCallback.DeferredMessage());

        ulong voiceChannelId = userVoiceState.ChannelId.GetValueOrDefault();
        AudioSession session = sessionManager.GetOrCreateSession(guild.Id, Context.Channel.Id);
        AudioConnection connection = session.Connection;
        AudioPlayer player = session.Player;
        
        CommandResult connectionResult = await connection.PostAsync(new ConnectCommand(voiceChannelId));

        if (connectionResult.Status is CommandStatus.InvalidVoice)
        {
            await FollowupAsync("Failed to connect to voice channel.");
            return;
        }

        if (connectionResult.Status is not CommandStatus.AlreadyConnected)
        {
            if (!session.PlayerMessage.HasMessage || (session.PlayerMessage.HasMessage && player.State is AudioPlayerState.Idle))
            {
                await session.PlayerMessage.SetAsync(Context.Interaction, player.State);
            }
        }

        CommandResult result = await player.PostAsync(new ResumeAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await FollowupAsync("Playback resumed.");
                break;
            
            case CommandStatus.AlreadyPlaying:
                await FollowupAsync("I am already resumed.");
                break;
            
            case CommandStatus.NothingToResume:
                await FollowupAsync("There is nothing to resume.");
                break;
        }
    }
    
    [SlashCommand("skip", "Skips current track.", Contexts = [InteractionContextType.Guild])]
    public async Task SkipAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new SkipAudioCommand());

        if (result.Status is CommandStatus.NothingToSkip)
        {
            await FollowupAsync("There is nothing to skip.");
        }
        else
        {
            await FollowupAsync("Skipped current track.");
        }
    }

    [SlashCommand("rewind", "Rewinds to the previous track.", Contexts = [InteractionContextType.Guild])]
    public async Task RewindAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new RewindAudioCommand());

        if (result.Status is CommandStatus.NothingToRewind)
        {
            await FollowupAsync("There is no previous track to rewind to.");
        }
        else
        {
            await FollowupAsync("Rewound to previous track.");
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
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new SeekAudioCommand(time));

        switch (result.Status)
        {
            case CommandStatus.Success:
                await FollowupAsync($"Seeking to {timestamp}");
                break;
            
            case CommandStatus.NothingToSeek:
                await FollowupAsync("No track is currently playing to seek.");
                break;
            
            case CommandStatus.UnableToSeek:
                await FollowupAsync($"Unable to seek to {timestamp}");
                break;
        }
    }

    [SlashCommand("loop", "Loops current track.", Contexts = [InteractionContextType.Guild])]
    public async Task LoopAsync(LoopMode? mode = null)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        
        CommandResult result = await session.Player.PostAsync(new LoopAudioCommand(mode));
        await FollowupAsync($"Loop mode set to {result.Data}.");
    }

    [SlashCommand("queue", "List queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task QueueAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        IReadOnlyList<Track> queue = session.Player.Queue.Next;
        PaginatorSession<Track> paginatorSession = new(new Paginator<Track>(queue, 10));

        EmbedProperties embed = EmbedHelper.Queue(session.Player.Queue.CurrentTrack, (GuildUser)Context.User, paginatorSession.Paginator);
        
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .AddEmbeds(embed)
            .AddComponents(ComponentsHelper.CreateQueuePaginationButtonsComponent(paginatorSession.Paginator))));
        
        RestMessage message = await GetResponseAsync();
        
        paginatorManager
            .Add(Context.Guild!.Id, message, paginatorSession, async msg => await msg
            .ModifyAsync(options => options.WithComponents([])));
    }
    
    [SlashCommand("history", "List all played and queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task HistoryAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        IReadOnlyList<Track> history = session.Player.Queue.Previous;
        PaginatorSession<Track> paginatorSession = new(new Paginator<Track>(history, 10));
        EmbedProperties embed = EmbedHelper.History((GuildUser)Context.User, paginatorSession.Paginator);
        
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .AddEmbeds(embed)
            .AddComponents(ComponentsHelper.CreateHistoryPaginationButtonsComponent(paginatorSession.Paginator))));
        
        RestMessage message = await GetResponseAsync();
        
        paginatorManager
            .Add(Context.Guild!.Id, message, paginatorSession, async msg => await msg
            .ModifyAsync(options => options.WithComponents([])));
    }

    [SlashCommand("shuffle", "Shuffles all queued tracks.", Contexts = [InteractionContextType.Guild])]
    public async Task ShuffleAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new ShuffleAudioCommand());

        if (result.Status is CommandStatus.NotEnoughTracksToShuffle)
        {
            await FollowupAsync("There is nothing to shuffle.");
        }
        else
        {
            await FollowupAsync("Shuffled the queue.");
        }
    }

    [SlashCommand("clear", "Clears the queue.", Contexts = [InteractionContextType.Guild])]
    public async Task ClearAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        CommandResult result = await session.Player.PostAsync(new ClearAudioCommand());

        if (result.Status is CommandStatus.NothingToClear)
        {
            await FollowupAsync("There is nothing to clear.");
        }
        else
        {
            await FollowupAsync("Queue cleared.");
        }
    }

    [SlashCommand("nowplaying", "Gets the currently playing track.", Contexts = [InteractionContextType.Guild])]
    public async Task NowPlayingAsync()
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        AudioPlayer player = session.Player;

        if (!player.Queue.HasCurrent)
        {
            await RespondAsync(InteractionCallback.Message("Nothing is currently playing."));
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());

        if (session.PlayerMessage.HasMessage)
        {
            await session.PlayerMessage.InvalidateAsync();
        }

        await session.PlayerMessage.SetAsync(Context.Interaction, player.State);
    }
    
    [SlashCommand("volume", "Set playback volume.", Contexts = [InteractionContextType.Guild])]
    public async Task VolumeAsync([SlashCommandParameter(MinValue = 0, MaxValue = 100)] int value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        
        await session.Player.PostAsync(new VolumeAudioCommand(value));
        await FollowupAsync($"Volume set to {value}%");
    }

    [SlashCommand("speed", "Set playback speed.", Contexts = [InteractionContextType.Guild])]
    public async Task SpeedAsync([SlashCommandParameter(MinValue = 0.25, MaxValue = 2)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        
        await session.Player.PostAsync(new SpeedAudioCommand(value));
        await FollowupAsync($"Speed set to {value.ToString(CultureInfo.InvariantCulture)}x");
    }

    [SlashCommand("pitch", "Set playback pitch.", Contexts = [InteractionContextType.Guild])]
    public async Task PitchAsync([SlashCommandParameter(MinValue = 0.25, MaxValue = 2)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());

        await session.Player.PostAsync(new PitchAudioCommand(value));
        await FollowupAsync($"Pitch set to {value.ToString(CultureInfo.InvariantCulture)}x");
    }

    [SlashCommand("rate", "Set playback rate.", Contexts = [InteractionContextType.Guild])]
    public async Task RateAsync([SlashCommandParameter(MinValue = 0.25, MaxValue = 2)] float value)
    {
        if (await GetAudioSessionAsync() is not AudioSession session)
        {
            return;
        }
        
        await RespondAsync(InteractionCallback.DeferredMessage());
        
        await session.Player.PostAsync(new RateAudioCommand(value));
        await FollowupAsync($"Rate set to {value.ToString(CultureInfo.InvariantCulture)}x");
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
            if (Context.Channel.Id == session.PlayerMessage.BoundChannelId)
            {
                return session;
            }
            
            string name = guild.Channels[session.PlayerMessage.BoundChannelId!.Value].Name;
            await RespondAsync(InteractionCallback.Message($"Use commands in #{name}."));
                
            return null;
        }
        
        await RespondAsync(InteractionCallback.Message("Nothing is playing."));
        return null;
    }
}
