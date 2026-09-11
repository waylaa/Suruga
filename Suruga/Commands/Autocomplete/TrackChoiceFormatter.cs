using NetCord.Rest;
using Suruga.Primitives;

namespace Suruga.Commands.Autocomplete;

internal static class TrackChoiceFormatter
{
    private const int MaxChoices = 25;
    private const int MaxLabelLength = 100;

    internal static IEnumerable<ApplicationCommandOptionChoiceProperties> Format(TrackSet set)
        => set.Tracks.Take(MaxChoices).Select(track => new ApplicationCommandOptionChoiceProperties(TruncateLabel(track), track.Uri));

    private static string TruncateLabel(Track track)
    {
        string label = $"{track.Title} - {track.Author}";

        return label.Length > MaxLabelLength
            ? string.Concat(label.AsSpan(0, MaxLabelLength - 3), "...")
            : label;
    }
}
