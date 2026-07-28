using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Persistence;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioPlayerFactory
(
	TrackResolverRouter trackResolverRouter,
	TrackStreamResolverRouter streamResolverRouter,
	ReadOnlyAudioByteStreamFactory byteStreamFactory,
	TrackQueueStateRepository repository,
	ILoggerFactory loggerFactory,
	IVoiceLogger voiceLogger
)
{
	internal AudioPlayer Create(GatewayClient client, ulong guildId)
	{
		return new AudioPlayer
		(
			guildId,
			trackResolverRouter,
			new AudioPlaybackEngine
			(
				new AudioConnection(guildId, client, voiceLogger),
				streamResolverRouter,
				byteStreamFactory,
				loggerFactory
			),
			repository
		);
	}
}
