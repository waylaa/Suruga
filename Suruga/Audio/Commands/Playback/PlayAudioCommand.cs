using Suruga.Primitives;

namespace Suruga.Audio.Commands.Playback;

internal sealed record PlayAudioCommand(TrackSet Tracks) : PlaybackCommand;
