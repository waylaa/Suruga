namespace Suruga.Primitives;

/// <summary>
/// Specifies the type of input source that can be resolved into audio tracks.
/// </summary>
internal enum InputType
{
    /// <summary>
    /// A path to a local file on disk.
    /// </summary>
    LocalFile,

    /// <summary>
    /// A YouTube video URI.
    /// </summary>
    YoutubeVideoUrl,

    /// <summary>
    /// A YouTube video ID.
    /// </summary>
    YoutubeVideoId,

    /// <summary>
    /// A YouTube playlist URI.
    /// </summary>
    YoutubePlaylist,

    /// <summary>
    /// A YouTube search query.
    /// </summary>
    YoutubeSearch,
}
