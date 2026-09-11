using NetCord.Rest;
using Suruga.Audio.Primitives;
using Suruga.Helpers;
using Suruga.Primitives;

namespace Suruga.Audio;

internal static class PlayerMessagePresenter
{
    internal static Presentation Resolve(AudioPlayerState state, Track? track, Exception? error) => state switch
    {
        AudioPlayerState.Playing when track is not null =>
            new Presentation(EmbedHelper.NowPlaying(track), [ComponentsHelper.CreatePlayerControlsComponent(false)], true),

        AudioPlayerState.Paused when track is not null =>
            new Presentation(EmbedHelper.Paused(track), [ComponentsHelper.CreatePlayerControlsComponent(true)], true),

        AudioPlayerState.Idle when track is not null && error is not null =>
            new Presentation(EmbedHelper.Errored(track), [], true),

        AudioPlayerState.Idle =>
            new Presentation(null, [], true),

        _ => new Presentation(null, [], false)
    };
    
    internal readonly record struct Presentation(EmbedProperties? Embed, IReadOnlyList<IMessageComponentProperties> Components, bool ShouldUpdate);
}
