namespace Suruga.Audio.Commands.Playback;

internal sealed record SeekAudioCommand(TimeSpan Timestamp) : PlaybackCommand;
