using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Audio.Commands.Playback;
using Suruga.Audio.Primitives;
using Suruga.Commands.Extensions;

namespace Suruga.Commands.Interactions;

internal sealed class TrackInteractionModule(AudioSessionManager sessionManager) : ComponentInteractionModule<ComponentInteractionContext>
{
    [ComponentInteraction("player_loop_toggle")]
    public async Task ToggleLoopOnCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }

        CommandResult result = await session.Player.PostAsync(new LoopAudioCommand(null));

        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithContent($"Loop mode set to {result.Data}.")
            .WithFlags(MessageFlags.Ephemeral)));
    }

    [ComponentInteraction("player_pause")]
    public async Task PauseCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }
        
        CommandResult result = await session.Player.PostAsync(new PauseAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Playback paused.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
            
            case CommandStatus.AlreadyPaused:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("There is nothing to pause.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
        }
    }

    [ComponentInteraction("player_resume")]
    public async Task ResumeCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }

        CommandResult result = await session.Player.PostAsync(new ResumeAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Playback resumed.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
            
            case CommandStatus.AlreadyPlaying:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("There is nothing to resume.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
        }
    }

    [ComponentInteraction("player_skip")]
    public async Task SkipCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }
        
        CommandResult result = await session.Player.PostAsync(new SkipAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Track skipped.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
            
            case CommandStatus.NothingToSkip:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("There is nothing to skip.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
        }
    }

    [ComponentInteraction("player_stop")]
    public async Task StopCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }
        
        CommandResult result = await session.Player.PostAsync(new StopAudioCommand());

        switch (result.Status)
        {
            case CommandStatus.Success:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Playback stopped.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
            
            case CommandStatus.AlreadyStopped:
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("There is nothing to stop.")
                    .WithFlags(MessageFlags.Ephemeral)));
                break;
        }
    }
}
