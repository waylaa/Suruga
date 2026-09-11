using System.Globalization;
using System.Text.Json;

namespace Suruga.Resolvers.Extensions;

internal static class JsonValueConversionExtensions
{
    internal static TimeSpan? GetTimeSpanSeconds(this JsonElement element)
        => TryGetInt32(element, out int seconds) ? TimeSpan.FromSeconds(seconds) : null;

    internal static TimeSpan? GetTimeSpan(this JsonElement element)
    {
        string? str = element.ValueKind is JsonValueKind.String ? element.GetString() : element.GetRawText();

        if (string.IsNullOrWhiteSpace(str))
        {
            return null;
        }

        Span<Range> parts = stackalloc Range[3];

        return str.Split(parts, ':') switch
        {
            2 when int.TryParse(str.AsSpan()[parts[0]], out int m) && int.TryParse(str.AsSpan()[parts[1]], out int s)
                => new TimeSpan(0, m, s),
            3 when int.TryParse(str.AsSpan()[parts[0]], out int h) && int.TryParse(str.AsSpan()[parts[1]], out int m) && int.TryParse(str.AsSpan()[parts[2]], out int s)
                => new TimeSpan(h, m, s),
            _ => null
        };
    }

    internal static T? ToValue<T>(this JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined)
        {
            return default;
        }

        if (typeof(T) == typeof(string))
        {
            string? str = element.ValueKind is JsonValueKind.String ? element.GetString() : element.GetRawText();
            return (T?)(object?)str;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True when typeof(T) == typeof(bool) => (T)(object)true,
            JsonValueKind.False when typeof(T) == typeof(bool) => (T)(object)false,
            JsonValueKind.Number or JsonValueKind.String => typeof(T) switch
            {
                _ when typeof(T) == typeof(int) && TryGetInt32(element, out int i) => (T)(object)i,
                _ when typeof(T) == typeof(long) && TryGetInt64(element, out long l) => (T)(object)l,
                _ when typeof(T) == typeof(double) && TryGetDouble(element, out double d) => (T)(object)d,
                _ => default
            },
            _ => default
        };
    }

    private static bool TryGetInt32(JsonElement element, out int value)
        => element.ValueKind is JsonValueKind.Number
            ? element.TryGetInt32(out value)
            : int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryGetInt64(JsonElement element, out long value)
        => element.ValueKind is JsonValueKind.Number
            ? element.TryGetInt64(out value)
            : long.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryGetDouble(this JsonElement element, out double value)
        => element.ValueKind is JsonValueKind.Number
            ? element.TryGetDouble(out value)
            : double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
