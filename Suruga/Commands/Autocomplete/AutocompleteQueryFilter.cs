namespace Suruga.Commands.Autocomplete;

internal static class AutocompleteQueryFilter
{
    internal static bool ShouldSkip(string? query)
        => string.IsNullOrWhiteSpace(query) || IsFilePath(query) || Uri.IsWellFormedUriString(query, UriKind.Absolute);

    internal static string NormalizeForResolver(string query)
        => query.StartsWith("yt") || query.StartsWith("youtube") ? query : $"yt:{query}";

    private static bool IsFilePath(string query)
    {
        if (query.StartsWith('/') || query.StartsWith('~'))
        {
            return true;
        }

        if (query is [_, ':', ..] && char.IsLetter(query[0]))
        {
            return true;
        }

        if (query.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        int dot = query.LastIndexOf('.');
        return dot > 0 && dot < query.Length - 1 && !query.Contains(' ');
    }
}
