using System.Net;
using NetCord.Rest;
using Suruga.Audio.Events;
using Suruga.Audio.Primitives;
using Suruga.Helpers;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class PlayerMessageUpdater
{
	internal bool HasMessage => _boundMessage is not null;
	
	private RestMessage? _boundMessage;
	
	internal void Set(RestMessage message)
		=> _boundMessage = message;

	internal async Task InvalidateAsync()
	{
		if (_boundMessage is not null)
		{
			await _boundMessage.ModifyAsync(options => options.WithComponents([]));
		}
		
		_boundMessage = null;
	}

	internal async Task UpdateAsync(PlayerStateChangedEventArgs args)
	{
		if (_boundMessage is null)
		{
			// Do not respond on an invalidated player message. This should generally not happen.
			return;
		}

		try
		{
			Track? track = args.CurrentTrack;
			
			switch (args.State)
			{
				case AudioPlaybackState.Playing when track is not null:
					await _boundMessage.ModifyAsync(options => options
							.WithEmbeds([EmbedHelper.NowPlaying(track)])
							.WithComponents([ComponentsHelper.CreatePlayerControlsComponent(false)]));
					break;
				
				case AudioPlaybackState.Paused when track is not null:
					await _boundMessage.ModifyAsync(options => options
						.WithEmbeds([EmbedHelper.Paused(track)])
						.WithComponents([ComponentsHelper.CreatePlayerControlsComponent(true)]));
					break;
				
				case AudioPlaybackState.Idle when track is not null && args.HasErrored:
					await _boundMessage.ModifyAsync(options => options
						.WithEmbeds([EmbedHelper.Errored(track)])
						.WithComponents([]));
					break;
				
				case AudioPlaybackState.Idle:
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
}
