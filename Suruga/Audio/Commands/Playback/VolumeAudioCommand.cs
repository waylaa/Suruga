namespace Suruga.Audio.Commands.Playback;

internal sealed record VolumeAudioCommand(int Value) : PlaybackCommand;
