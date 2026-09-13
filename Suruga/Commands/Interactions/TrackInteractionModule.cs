using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Audio.Primitives;

namespace Suruga.Commands.Interactions;

internal sealed class TrackInteractionModule(AudioSessionManager sessionManager) : ComponentInteractionModule<ComponentInteractionContext>
{
    [ComponentInteraction("player_loop_toggle")]
    public async Task ToggleLoopOnCurrentTrack()
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            return;
        }

        CommandResult result = await session.Player.LoopAsync();
        await RespondEphemeralAsync($"Loop mode set to {result.Data}.");
    }

    [ComponentInteraction("player_pause")]
    public Task PauseCurrentTrack()
        => ExecuteAsync(async player => await player.PauseAsync(), "Playback paused.");

    [ComponentInteraction("player_resume")]
    public Task ResumeCurrentTrack()
        => ExecuteAsync(async player => await player.ResumeAsync(), "Playback resumed.");
    
    [ComponentInteraction("player_rewind")]
    public Task RewindToPreviousTrack()
        => ExecuteAsync(async player => await player.RewindAsync(), "Playback rewinding.");

    [ComponentInteraction("player_skip")]
    public Task SkipCurrentTrack()
        => ExecuteAsync(async player => await player.SkipAsync(), "Track skipped.");

    [ComponentInteraction("player_stop")]
    public Task StopCurrentTrack()
        => ExecuteAsync(async player => await player.StopAsync(), "Playback stopped.");

    private async Task ExecuteAsync(Func<AudioPlayer, Task<CommandResult>> onExecute, string successMessage)
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("I must be connected to a voice channel.")
                .WithFlags(MessageFlags.Ephemeral)));

            return;
        }

        CommandResult result = await onExecute(session.Player);
        
        string response = result.Status switch
        {
            CommandStatus.Success => successMessage,
            CommandStatus.NothingToSkip => "There is nothing to skip.",
            CommandStatus.NothingToRewind => "There is no previous track to rewind to.",
            CommandStatus.NothingToPause => "There is nothing to pause.",
            CommandStatus.AlreadyPaused => "Playback is already paused.",
            CommandStatus.NothingToResume => "There is nothing to resume.",
            CommandStatus.AlreadyPlaying => "Playback is already active.",
            CommandStatus.AlreadyStopped => "There is nothing to stop.",
            _ => "Command failed."
        };
        
        await RespondEphemeralAsync(response);
    }

    private Task<InteractionCallbackResponse?> RespondEphemeralAsync(string content)
        => RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral)));
}
