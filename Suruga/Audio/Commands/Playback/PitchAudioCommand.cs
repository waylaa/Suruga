namespace Suruga.Audio.Commands.Playback;

internal sealed record PitchAudioCommand(double Value) : PlaybackCommand;
