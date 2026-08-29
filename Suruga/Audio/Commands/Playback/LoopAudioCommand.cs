using Suruga.Audio.Primitives;

namespace Suruga.Audio.Commands.Playback;

internal sealed record LoopAudioCommand(LoopMode? Mode) : PlaybackCommand;
