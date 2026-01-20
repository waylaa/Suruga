using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Suruga.Commands.Autocomplete;

internal sealed class TrackResultsAutocompleteProvider : IAutocompleteProvider<AutocompleteInteractionContext>
{
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync
    (
        ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context
    )
    {
        if (option.Value is not string rawQuery)
        {
            return [];
        }

        string query = rawQuery.Trim();

        if (query.Length == 0)
        {
            return [];
        }

        // todo: impl
        return [];
    }
}
