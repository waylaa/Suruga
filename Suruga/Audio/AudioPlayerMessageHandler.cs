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
            // Do not respond on an invalidated player message. This should generally not happen.
            return;
        }

        try
        {
            switch (state)
            {
                case AudioPlayerState.Playing when track is not null:
                    await _boundMessage.ModifyAsync(options => options
                        .WithEmbeds([EmbedHelper.NowPlaying(track)])
                        .WithComponents([ComponentsHelper.CreatePlayerControlsComponent(false)]));
                    break;
				
                case AudioPlayerState.Paused when track is not null:
                    await _boundMessage.ModifyAsync(options => options
                        .WithEmbeds([EmbedHelper.Paused(track)])
                        .WithComponents([ComponentsHelper.CreatePlayerControlsComponent(true)]));
                    break;
				
                case AudioPlayerState.Idle when track is not null && error is not null:
                    await _boundMessage.ModifyAsync(options => options
                        .WithEmbeds([EmbedHelper.Errored(track)])
                        .WithComponents([]));
                    break;
				
                case AudioPlayerState.Idle:
                    await _boundMessage.ModifyAsync(options => options.WithComponents([]));
                    break;
				
                default: return;
            }
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            _boundMessage = null; // A user deleted the player message, invalidate.
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
