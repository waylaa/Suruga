using System.Net;
using NetCord;
using NetCord.Rest;
using Suruga.Audio.Primitives;
using Suruga.Common;
using Suruga.Helpers;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class AudioPlayerMessage
{
    internal ulong? BoundChannelId => _message?.ChannelId;
    
    internal bool HasMessage => _message is not null;

    private RestMessage? _message;

    internal async Task BindAsync(Interaction interaction, AudioPlayerState currentState)
    {
        if (_message is not null)
        {
            Logger.Debug<AudioPlayerMessage>("Rebinding player message and clearing controls on the previous one.");
            await TryClearControlsAsync(_message);
        }

        _message = await interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
            .WithEmbeds([])
            .WithComponents([ComponentsHelper.CreatePlayerControlsComponent(currentState is AudioPlayerState.Paused)]));
        
        Logger.Debug<AudioPlayerMessage>($"Player message bound in channel {_message.ChannelId}");
    }
    
    internal async Task RefreshAsync(AudioPlayerState state, Track? track, Exception? error)
    {
        if (_message is not RestMessage message)
        {
            return;
        }

        PlayerMessagePresenter.Presentation presentation = PlayerMessagePresenter.Resolve(state, track);

        if (!presentation.ShouldUpdate)
        {
            Logger.Trace<AudioPlayerMessage>($"Player message refresh skipped for state {state}");
            return;
        }

        try
        {
            await message.ModifyAsync(options =>
            {
                options.WithComponents(presentation.Components);

                if (presentation.Embed is not null)
                {
                    options.WithEmbeds([presentation.Embed]);
                }
            });
            
            Logger.Trace<AudioPlayerMessage>($"Player message refreshed for state {state}.");
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            Logger.Info<AudioPlayerMessage>("Unbinding player message because it was deleted externally.");
            _message = null;
        }
    }

    internal async Task InvalidateAsync()
    {
        if (_message is RestMessage message)
        {
            await TryClearControlsAsync(message);
        }
        
        _message = null;
        Logger.Debug<AudioPlayerMessage>("Player message invalidated.");
    }

    private static async Task TryClearControlsAsync(RestMessage message)
    {
        try
        {
            await message.ModifyAsync(options => options.WithComponents([]));
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            Logger.Debug<AudioPlayerMessage>("Tried to clear controls on a player message that was already deleted.");
        }
    }
    
    private static class PlayerMessagePresenter
    {
        internal static Presentation Resolve(AudioPlayerState state, Track? track = null) => (state, track) switch
        {
            (AudioPlayerState.Playing, not null) => new Presentation(EmbedHelper.NowPlaying(track), [ComponentsHelper.CreatePlayerControlsComponent(false)], true),
            (AudioPlayerState.Paused, not null) => new Presentation(EmbedHelper.Paused(track), [ComponentsHelper.CreatePlayerControlsComponent(true)], true),
            (AudioPlayerState.Idle, not null) => new Presentation(EmbedHelper.Errored(track), [], true),
            (AudioPlayerState.Idle, null) => new Presentation(null, [], true),
            _ => new Presentation(null, [], false)
        };

        internal readonly record struct Presentation(EmbedProperties? Embed, IReadOnlyList<IMessageComponentProperties> Components, bool ShouldUpdate);
    }
}
