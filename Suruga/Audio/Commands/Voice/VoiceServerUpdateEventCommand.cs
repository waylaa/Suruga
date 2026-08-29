namespace Suruga.Audio.Commands.Voice;

internal sealed record VoiceServerUpdateEventCommand(string? Endpoint, string Token) : VoiceEventCommand;
