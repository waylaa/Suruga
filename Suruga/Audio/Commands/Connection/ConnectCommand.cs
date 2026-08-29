namespace Suruga.Audio.Commands.Connection;

internal sealed record ConnectCommand(ulong VoiceChannelId) : ConnectionCommand;
