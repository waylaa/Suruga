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
    
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        string? query = option.Value?.Trim();

        if (query is null || AutocompleteQueryFilter.ShouldSkip(query) || !await Debouncer.WaitAsync(context.User.Id))
        {
            return [];
        }

        query = AutocompleteQueryFilter.NormalizeForResolver(query);
        TrackRequestContext requestContext = TrackRequestContext.FromUser((GuildUser)context.User);
        Result<TrackSet> resolveResult = await _resolver.ResolveAsync(query, requestContext);

        return resolveResult.TryGetValue(out TrackSet? set) ? TrackChoiceFormatter.Format(set) : [];
    }
}
