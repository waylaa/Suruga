namespace Suruga.Primitives;

/// <summary>
/// Represents a local file-based stream source.
/// </summary>
/// <param name="FilePath">The absolute path to the audio file.</param>
internal sealed record LocalStreamSource(string FilePath) : StreamSource;
