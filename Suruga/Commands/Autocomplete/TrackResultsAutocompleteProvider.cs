using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Primitives;
using Suruga.Resolvers;

namespace Suruga.Commands.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for track search queries.
/// </summary>
/// <param name="services">A service provider.</param>
public sealed class TrackResultsAutocompleteProvider(IServiceProvider services) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private static readonly Debouncer<ulong> Debouncer = new(TimeSpan.FromMilliseconds(700));

    // Workaround for NetCord only scanning public types which makes us not being able
    // to constructor inject YoutubeTrackResolver due to TrackResultsAutocompleteProvider
    // being forcibly public and YoutubeTrackResolver being internal.
    private readonly TrackResolverRouter _resolver = services.GetRequiredService<TrackResolverRouter>();
    
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync
    (
        ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context
    )
    {
        string? query = option.Value?.Trim();

        if (string.IsNullOrWhiteSpace(query) || IsFilePath(query) || Uri.IsWellFormedUriString(query, UriKind.Absolute))
        {
            return [];
        }

        ulong userId = context.User.Id;

        // Cancel the previous pending request for this user and start a new one.
        if (!await Debouncer.WaitAsync(userId))
        {
            return [];
        }

        // Append yt/youtube prefix if the user did not append it themselves.
        if (!query.StartsWith("yt") && !query.StartsWith("youtube"))
        {
            query = $"yt:{query}";
        }

        TrackRequestContext requestContext = TrackRequestContext.FromUser((GuildUser)context.User);
        Result<TrackSet> resolveResult = await _resolver.ResolveAsync(query, requestContext);

        if (!resolveResult.TryGetValue(out TrackSet? set))
        {
            return [];
        }
        
        const int maxDiscordAutocompleteChoices = 25;
        List<ApplicationCommandOptionChoiceProperties> choices = new(maxDiscordAutocompleteChoices);

        foreach (Track track in set.Tracks.Take(maxDiscordAutocompleteChoices))
        {
            string label = $"{track.Title} - {track.Author}";

            if (label.Length > 100)
            {
                label = string.Concat(label.AsSpan(0, 97), "...");
            }
            
            choices.Add(new ApplicationCommandOptionChoiceProperties(label, track.Uri));
        }

        return choices;
    }

    private static bool IsFilePath(string query)
    {
        // Absolute Unix path.
        if (query.StartsWith('/') || query.StartsWith('~'))
        {
            return true;
        }
        
        // Absolute Windows path (e.g. C:\, \\server\share).
        if (query is [_, ':', ..] && char.IsLetter(query[0]))
        {
            return true;
        }

        if (query.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }
        
        // Has a file extension and no spaces (e.g. "track.wav").
        int dot = query.LastIndexOf('.');
        
        return dot > 0 && dot < query.Length - 1 && !query.Contains(' ');
    }
}
