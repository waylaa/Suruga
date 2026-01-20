namespace Suruga.Primitives;

internal sealed class AudioSource
{
    internal static AudioSource Empty { get; } = new()
    {
        Platform = AudioPlatform.Unknown,
        Tracks = [],
        IsPlaylist = false
    };

    internal required AudioPlatform Platform { get; init; }

    internal required IReadOnlyList<AudioTrack> Tracks { get; init; }

    internal required bool IsPlaylist { get; init; }

    internal static AudioSource FromSingle(AudioPlatform platform, AudioTrack track) => new()
    {
        Platform = platform,
        Tracks = [track],
        IsPlaylist = false
    };

    internal static AudioSource FromPlaylist(AudioPlatform platform, IReadOnlyList<AudioTrack> tracks) => new()
    {
        Platform = platform,
        Tracks = tracks,
        IsPlaylist = true
    };
}
