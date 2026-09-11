using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Suruga.Resolvers.Classification;

namespace Suruga.Resolvers.Primitives;

internal sealed class Input : IParsable<Input>
{
    internal InputType Type { get; }
    
    internal string Value { get; }

    private static readonly IReadOnlyList<IInputClassifier> Classifiers = [new LocalFileInputClassifier(), new YoutubeInputClassifier()];

    private Input(InputType type, string value)
    {
        Type = type;
        Value = value;
    }

    internal static Input Parse(string input)
        => Parse(input, CultureInfo.InvariantCulture);

    internal static bool TryParse(string input, [NotNullWhen(true)] out Input? result)
        => TryParse(input, CultureInfo.InvariantCulture, out result);

    public static Input Parse(string s, IFormatProvider? provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        foreach (IInputClassifier classifier in Classifiers)
        {
            if (classifier.TryClassify(s, out InputType type, out string normalizedValue))
            {
                return new Input(type, normalizedValue);
            }
        }

        throw new InvalidOperationException($"Could not parse the specified input: {s}");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Input result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = null;
            return false;
        }

        foreach (IInputClassifier classifier in Classifiers)
        {
            if (classifier.TryClassify(s, out InputType type, out string normalizedValue))
            {
                result = new Input(type, normalizedValue);
                return true;
            }
        }

        result = null;
        return false;
    }
}
