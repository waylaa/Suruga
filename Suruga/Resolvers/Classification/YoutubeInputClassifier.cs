using System.Collections.Specialized;
using System.Web;
using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers.Classification;

internal sealed class YoutubeInputClassifier : IInputClassifier
{
    public bool TryClassify(string rawValue, out InputType type, out string normalizedValue)
    {
        type = default;
        normalizedValue = string.Empty;

        if (TryClassifySearchPrefix(rawValue, out type, out normalizedValue))
        {
            return true;
        }

        if (TryClassifyVideoId(rawValue, out type, out normalizedValue))
        {
            return true;
        }

        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out Uri? uri) || !IsYoutubeHost(uri))
        {
            return false;
        }

        return TryClassifyUrl(rawValue, uri, out type, out normalizedValue);
    }

    private static bool TryClassifySearchPrefix(string value, out InputType type, out string normalizedValue)
    {
        foreach (string prefix in (string[])["yt:", "youtube:"])
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                type = InputType.YoutubeSearch;
                normalizedValue = value[prefix.Length..];
                return true;
            }
        }

        type = default;
        normalizedValue = string.Empty;
        return false;
    }

    private static bool TryClassifyVideoId(string value, out InputType type, out string normalizedValue)
    {
        if (value.Length == 11 && value.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
        {
            type = InputType.YoutubeVideoId;
            normalizedValue = value;
            return true;
        }

        type = default;
        normalizedValue = string.Empty;
        return false;
    }

    private static bool IsYoutubeHost(Uri uri)
    {
        string host = uri.Host.ToLowerInvariant();

        return host == "youtube.com" || host.EndsWith(".youtube.com") ||
               host == "youtu.be" ||
               host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com");
    }

    private static bool TryClassifyUrl(string rawValue, Uri uri, out InputType type, out string normalizedValue)
    {
        NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
        string? id = query["v"];
        string? playlistId = query["list"];

        if (!string.IsNullOrEmpty(playlistId))
        {
            type = InputType.YoutubePlaylist;
            normalizedValue = rawValue;
            return true;
        }

        if (string.IsNullOrEmpty(id))
        {
            // e.g. youtu.be/VIDEO_ID, /shorts/VIDEO_ID, /embed/VIDEO_ID.
            if (uri.Segments.Length < 2)
            {
                type = default;
                normalizedValue = string.Empty;
                return false;
            }

            type = InputType.YoutubeVideoId;
            normalizedValue = uri.Segments[1];
            return true;
        }

        type = InputType.YoutubeVideoUrl;
        normalizedValue = rawValue;
        return true;
    }
}
