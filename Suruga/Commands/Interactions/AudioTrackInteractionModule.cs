using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Extensions;

namespace Suruga.Commands.Interactions;

internal sealed class AudioTrackInteractionModule(AudioSessionManager sessionManager) : ComponentInteractionModule<ComponentInteractionContext>
{
    [ComponentInteraction("player_loop_toggle")]
    public async Task ToggleLoopOnCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
            return;
        }

        await session.Player.LoopAsync();
        
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithContent($"Loop mode set to {session.Player.Queue.LoopMode.GetDescription()}.")
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

        if (session.Player.State is AudioPlaybackState.Playing)
        {
            await session.Player.PauseAsync();
            
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Playback paused.")
                .WithFlags(MessageFlags.Ephemeral)));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("There is nothing to pause.")
                .WithFlags(MessageFlags.Ephemeral)));
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
        
        if (session.Player.State is AudioPlaybackState.Paused)
        {
            await session.Player.ResumeAsync();
            
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Playback resumed.")
                .WithFlags(MessageFlags.Ephemeral)));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("There is nothing to resume.")
                .WithFlags(MessageFlags.Ephemeral)));
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

        if (session.Player.State is AudioPlaybackState.Playing)
        {
            await session.Player.SkipAsync();
            
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Track skipped.")
                .WithFlags(MessageFlags.Ephemeral)));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("There is nothing to skip.")
                .WithFlags(MessageFlags.Ephemeral)));
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

        if (session.Player.State is AudioPlaybackState.Playing)
        {
            await session.Player.StopAsync();
            
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Playback stopped.")
                .WithFlags(MessageFlags.Ephemeral)));
        }
        else
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("There is nothing to stop.")
                .WithFlags(MessageFlags.Ephemeral)));
        }
    }
}
