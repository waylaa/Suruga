using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class PlayerStateNotifier
{
    internal event Func<AudioPlayerState, Track?, Exception?, Task>? StateChanged;

    internal Task NotifyAsync(AudioPlayerState state, Track? track, Exception? error)
        => StateChanged is not null ? StateChanged(state, track, error) : Task.CompletedTask;
}
