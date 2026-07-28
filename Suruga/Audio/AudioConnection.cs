using System.Diagnostics.CodeAnalysis;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio;

/// <summary>
/// Represents a managed connection to a Discord voice channel.
/// </summary>
internal sealed class AudioConnection : VolatileAsyncDisposable
{
	/// <summary>
	/// Gets the audio sink used to write PCM samples to the voice connection.
	/// </summary>
	internal AudioSink Sink { get; } = new();
	
	/// <summary>
	/// Gets the descriptor that tracks the current voice connection state.
	/// </summary>
	internal AudioConnectionDescriptor Descriptor { get; } = new();

	private readonly ulong _guildId;
	private readonly GatewayClient _gatewayClient;
	private readonly IVoiceLogger _voiceLogger;

	private readonly VolatileDisposableResource<VoiceClient> _voiceClientResource = new();
	private readonly VolatileAsyncDisposableResource<Stream> _voiceStreamResource = new();

	/// <summary>
	/// Initializes a new audio connection for the specified guild.
	/// </summary>
	/// <param name="guildId">The guild identifier.</param>
	/// <param name="gatewayClient">The Discord gateway client.</param>
	/// <param name="voiceLogger">Logger used for voice diagnostics.</param>
	internal AudioConnection(ulong guildId, GatewayClient gatewayClient, IVoiceLogger voiceLogger)
	{
		_guildId = guildId;
		_gatewayClient = gatewayClient;
		_voiceLogger = voiceLogger;
	}

	/// <summary>
	/// Connects to the specified voice channel, establishing or replacing the active voice session.
	/// </summary>
	/// <param name="voiceChannelId">The target voice channel ID.</param>
	/// <param name="token">A cancellation token.</param>
	internal async Task ConnectAsync(ulong voiceChannelId, CancellationToken token = default)
	{
		ThrowIfDisposed();

		if (Descriptor.IsValid &&
		    Descriptor.VoiceChannelId == voiceChannelId &&
		    _voiceClientResource.Current is not null &&
		    _voiceStreamResource.Current is not null)
		{
			return;
		}
		
		// Connect to the voice channel.
		await _gatewayClient.UpdateVoiceStateAsync(
			new VoiceStateProperties(_guildId, voiceChannelId), cancellationToken: token);

		await Descriptor.WaitUntilReadyAsync(token);
		await EstablishVoiceSessionAsync(token);
	}

	/// <summary>
	/// Disconnects from the current voice channel and releases all voice resources.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal async Task DisconnectAsync(CancellationToken token = default)
	{
		ThrowIfDisposed();
		
		if (!Descriptor.IsValid || _voiceClientResource.Current is null)
		{
			return;
		}

		await Sink.DetachAsync(token);
		await _voiceStreamResource.ClearAsync();
		await TeardownVoiceClientAsync(token);
		
		Descriptor.Invalidate();
		
		// Disconnect from the voice channel.
		await _gatewayClient.UpdateVoiceStateAsync(
			new VoiceStateProperties(_guildId, null), cancellationToken: token);
	}

	/// <summary>
	/// Reconnects the voice session using the last known connection descriptor.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal async Task ReconnectAsync(CancellationToken token = default)
	{
		ThrowIfDisposed();

		if (!Descriptor.IsValid)
		{
			await Descriptor.WaitUntilReadyAsync(token);
		}
		
		await Sink.DetachAsync(token);
		await TeardownVoiceClientAsync(token);
		await EstablishVoiceSessionAsync(token);
	}

	protected override async ValueTask DisposeCoreAsync()
	{
		await Sink.DisposeAsync();
		await _voiceStreamResource.ClearAsync();
		await TeardownVoiceClientAsync(CancellationToken.None);
	}

	private async Task EstablishVoiceSessionAsync(CancellationToken token)
	{
		VoiceClient voiceClient = new
		(
			Descriptor.UserId!.Value,
			Descriptor.SessionId!,
			Descriptor.Endpoint!,
			Descriptor.GuildId!.Value,
			Descriptor.VoiceChannelId!.Value,
			Descriptor.Token!,
			new VoiceClientConfiguration { Logger = _voiceLogger }
		);

		_voiceClientResource.Replace(voiceClient);

		await voiceClient.StartAsync(token);
		await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone), cancellationToken: token);

		Stream voiceStream = voiceClient.CreateVoiceStream();
		await _voiceStreamResource.ReplaceAsync(voiceStream);

		await Sink.AttachAsync(voiceStream, token);
	}

	private async Task TeardownVoiceClientAsync(CancellationToken token)
	{
		VoiceClient? currentVoiceClient = _voiceClientResource.Current;

		if (currentVoiceClient is not null)
		{
			try
			{
				await currentVoiceClient.CloseAsync(cancellationToken: token);
			}
			catch
			{
				// Ignore.
			}
		}
		
		_voiceClientResource.Clear();
	}

	/// <summary>
	/// Internal state tracker for the underlying voice connection.
	/// </summary>
	internal sealed class AudioConnectionDescriptor
	{
		/// <summary>
		/// Gets whether the descriptor contains a fully valid voice connection state.
		/// </summary>
		[MemberNotNullWhen(true,
			nameof(GuildId),
			nameof(UserId),
			nameof(VoiceChannelId),
			nameof(Endpoint),
			nameof(Token),
			nameof(SessionId))]
		internal bool IsValid =>
			GuildId.HasValue &&
			UserId.HasValue &&
			VoiceChannelId.HasValue &&
			!string.IsNullOrEmpty(Endpoint) &&
			!string.IsNullOrEmpty(Token) &&
			!string.IsNullOrEmpty(SessionId);
		
		internal ulong? GuildId { get; private set; }
		
		internal ulong? UserId { get; private set; }
		
		internal ulong? VoiceChannelId { get; private set; }

		internal string? Endpoint { get; private set; }

		internal string? Token { get; private set; }
		
		internal string? SessionId { get; private set; }
		
		private readonly AsyncManualResetEvent _readySignal = new();
		private readonly Lock _lock = new();

		internal AudioConnectionDescriptor()
			=> _readySignal.Reset(); // Set to unsignaled state.

		/// <summary>
		/// Waits until the descriptor has received all required voice connection data.
		/// </summary>
		/// <param name="token">A cancellation token.</param>
		internal async Task WaitUntilReadyAsync(CancellationToken token = default)
		{
			if (token.CanBeCanceled)
			{
				await _readySignal.WaitAsync(token: token);
				return;
			}

			using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(15));
			await _readySignal.WaitAsync(token: timeoutCts.Token);
		}
		
		internal void UpdateVoiceServer(string? endpoint, string? token)
		{
			using (_lock.EnterScope())
			{
				Endpoint = endpoint;
				Token = token;

				TrySetReady();
			}
		}
		
		internal void UpdateVoiceState(ulong? guildId, ulong? userId, ulong? voiceChannelId, string? sessionId)
		{
			using (_lock.EnterScope())
			{
				GuildId = guildId;
				UserId = userId;
				VoiceChannelId = voiceChannelId;
				SessionId = sessionId;

				TrySetReady();
			}
		}
		
		/// <summary>
		/// Invalidates the current connection state and resets readiness.
		/// </summary>
		internal void Invalidate()
		{
			using (_lock.EnterScope())
			{
				_readySignal.Reset();
				
				GuildId = null;
				UserId = null;
				VoiceChannelId = null;
				Endpoint = null;
				Token = null;
				SessionId = null;
			}
		}

		private void TrySetReady()
		{
			if (IsValid)
			{
				_readySignal.Set();
			}
		}
	}
}
