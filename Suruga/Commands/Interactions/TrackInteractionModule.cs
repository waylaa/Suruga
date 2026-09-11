using System.Diagnostics.CodeAnalysis;
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
        if (!TryGetSession(out AudioSession? session))
        {
            return;
        }

        CommandResult result = await session.Player.PostAsync(new LoopAudioCommand(null));
        await RespondEphemeralAsync($"Loop mode set to {result.Data}.");
    }

    [ComponentInteraction("player_pause")]
    public Task PauseCurrentTrack()
        => ExecuteAsync(new PauseAudioCommand(), "Playback paused.");

    [ComponentInteraction("player_resume")]
    public Task ResumeCurrentTrack()
        => ExecuteAsync(new ResumeAudioCommand(), "Playback resumed.");

    [ComponentInteraction("player_skip")]
    public Task SkipCurrentTrack()
        => ExecuteAsync(new SkipAudioCommand(), "Track skipped.");

    [ComponentInteraction("player_stop")]
    public Task StopCurrentTrack()
        => ExecuteAsync(new StopAudioCommand(), "Playback stopped.");

    private async Task ExecuteAsync(PlaybackCommand command, string successMessage)
    {
        if (!TryGetSession(out AudioSession? session))
        {
            return;
        }

        CommandResult result = await session.Player.PostAsync(command);
        string? message = result.Status is CommandStatus.Success ? successMessage : PlaybackCommandResponses.MessageFor(result.Status);

        if (message is not null)
        {
            await RespondEphemeralAsync(message);
        }
    }

    private bool TryGetSession([NotNullWhen(true)] out AudioSession? session)
    {
        if (sessionManager.TryGetSession(Context.Guild!.Id, out session))
        {
            return true;
        }

        _ = RespondAsync(InteractionCallback.Message(new InteractionMessageProperties().NotInVoiceChannelMessage()));
        return false;
    }

    private Task<InteractionCallbackResponse?> RespondEphemeralAsync(string content)
        => RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral)));
}
