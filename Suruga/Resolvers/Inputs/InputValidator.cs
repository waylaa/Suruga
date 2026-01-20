using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Web;

namespace Suruga.Resolvers.Inputs;

internal static class InputValidator
{
    private static readonly string[] SupportedFileExtensions =
    [
        ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".opus", ".aac",
        ".mp4", ".mkv"
    ];
    
    private static readonly string[] SupportedYoutubeDomains =
    [
        "youtube.com", "www.youtube.com", "youtu.be", "m.youtube.com", "music.youtube.com"
    ];
    
    internal static bool TryValidate(string? input, [NotNullWhen(true)] out InputType? type)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            type = null;
            return false;
        }

        if (ValidateIfFile(input))
        {
            type = InputType.LocalFile;
            return true;
        }

        if (ValidateIfYoutubeInput(input, out type))
        {
            return true;
        }

        type = null;
        return false;
    }

    private static bool ValidateIfFile(string input)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? fileUri) && fileUri.IsFile)
        {
            return true;
        }

        string extension = Path.GetExtension(input).ToLowerInvariant();
        return File.Exists(input) && SupportedFileExtensions.Contains(extension);
    }

    private static bool ValidateIfYoutubeInput(string input, [NotNullWhen(true)] out InputType? type)
    {
        if (input.Length == 11 && input.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            type = InputType.YoutubeId;
            return true;
        }
        
        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? youtubeUri))
        {
            if (SupportedYoutubeDomains.Any(d => youtubeUri.Host.Equals(d, StringComparison.OrdinalIgnoreCase)))
            {
                type = InputType.YoutubeUrl;
                return true;
            }

            NameValueCollection queries = HttpUtility.ParseQueryString(youtubeUri.Query);

            if (queries["search_query"] is not null)
            {
                type = InputType.YoutubeQuery;
                return true;
            }
        }

        type = null;
        return false;
    }
}