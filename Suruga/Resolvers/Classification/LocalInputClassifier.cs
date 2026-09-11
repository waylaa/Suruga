using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers.Classification;

internal sealed class LocalFileInputClassifier : IInputClassifier
{
    private static readonly string[] SupportedExtensions = [".wav", ".flac", ".opus", ".aac", ".ogg", ".mp3", ".m4a", ".mp4", ".mkv", ".webm"];

    public bool TryClassify(string rawValue, out InputType type, out string normalizedValue)
    {
        type = default;
        normalizedValue = rawValue;

        if (!Path.IsPathFullyQualified(rawValue))
        {
            return false;
        }

        string extension = Path.GetExtension(rawValue);

        if (string.IsNullOrWhiteSpace(extension) || !SupportedExtensions.Contains(extension) || !File.Exists(rawValue))
        {
            return false;
        }

        type = InputType.LocalFile;
        return true;
    }
}
