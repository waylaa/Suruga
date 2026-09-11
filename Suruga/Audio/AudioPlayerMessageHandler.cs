using System.Net;
using NetCord;
using NetCord.Rest;
using Suruga.Audio.Primitives;
using Suruga.Helpers;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class AudioPlayerMessageHandler
{
    internal ulong? BoundChannelId => _boundMessage?.ChannelId;
    
    internal bool HasMessage => _boundMessage is not null;
    
    private RestMessage? _boundMessage;

    internal async Task SetAsync(Interaction interaction, AudioPlayerState currentState)
    {
        if (_boundMessage is not null)
        {
            await _boundMessage.ModifyAsync(options => options.WithComponents([]));
            _boundMessage = null;
        }
        
        _boundMessage = await CreatePlayerMessageAsync(interaction, currentState);
    }
    
    internal async Task UpdateAsync(AudioPlayerState state, Track? track, Exception? error)
    {
        if (_boundMessage is null)
        {
            return;
        }

        PlayerMessagePresenter.Presentation presentation = PlayerMessagePresenter.Resolve(state, track, error);

        if (!presentation.ShouldUpdate)
        {
            return;
        }

        try
        {
            await _boundMessage.ModifyAsync(options =>
            {
                options.WithComponents(presentation.Components);

                if (presentation.Embed is not null)
                {
                    options.WithEmbeds([presentation.Embed]);
                }
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            _boundMessage = null;
        }
    }

    internal async Task InvalidateAsync()
    {
        if (_boundMessage is not null)
        {
            await _boundMessage.ModifyAsync(options => options.WithComponents([]));
        }
        
        _boundMessage = null;
    }
    
    private static Task<RestMessage> CreatePlayerMessageAsync(Interaction interaction, AudioPlayerState currentState)
    {
        return interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
            .WithEmbeds([])
            .WithComponents([ComponentsHelper.CreatePlayerControlsComponent(currentState is AudioPlayerState.Paused)]));
    }
}
