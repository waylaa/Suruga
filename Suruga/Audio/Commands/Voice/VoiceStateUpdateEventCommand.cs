namespace Suruga.Audio.Commands.Voice;

internal sealed record VoiceStateUpdateEventCommand(ulong GuildId, ulong UserId, ulong? ChannelId, string SessionId) : VoiceEventCommand;
