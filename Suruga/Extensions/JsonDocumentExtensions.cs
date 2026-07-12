using System.Globalization;
using System.Text.Json;

namespace Suruga.Extensions;

/// <summary>
/// Provides traversal and conversion extension methods for <see cref="JsonElement"/>.
/// </summary>
internal static class JsonDocumentExtensions
{
    /// <summary>
    /// Traverses a JSON object using a sequence of property names.
    /// </summary>
    /// <param name="current">The starting JSON element.</param>
    /// <param name="path">The property path to traverse.</param>
    /// <returns>The resolved element, or <see cref="JsonElement"/>.ValueKind = Undefined if not found.</returns>
    internal static JsonElement TraverseOrDefault(this JsonElement current, params ReadOnlySpan<string> path)
	{
		foreach (string segment in path)
		{
			if (current.ValueKind is not JsonValueKind.Object || !current.TryGetProperty(segment, out current))
			{
				return default;
			}
		}

		return current;
	}

    /// <summary>
    /// Traverses a single property.
    /// </summary>
    internal static JsonElement TraverseOrDefault(this JsonElement element, string property)
		=> element.ValueKind is JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) ? value : default;

    /// <summary>
    /// Traverses a path and converts the result to a value type.
    /// </summary>
    internal static T? TraverseValue<T>(this JsonElement element, params ReadOnlySpan<string> path)
		=> element.TraverseOrDefault(path).ToValue<T>();

    /// <summary>
    /// Traverses a property and converts the result to a value type.
    /// </summary>
    internal static T? TraverseValue<T>(this JsonElement element, string property)
		=> element.ValueKind is JsonValueKind.Undefined ? default : element.TraverseOrDefault(property).ToValue<T>();

    /// <summary>
    /// Enumerates a JSON array.
    /// </summary>
    internal static JsonElement.ArrayEnumerator? EnumerateArrayOrNull(this JsonElement element)
		=> element.ValueKind is JsonValueKind.Array ? element.EnumerateArray() : null;

    /// <summary>
    /// Gets an array element at the specified index or default.
    /// </summary>
    internal static JsonElement GetElementAtOrDefault(this JsonElement element, int index)
	{
		if (element.ValueKind is not JsonValueKind.Array)
		{
			return default;
		}
		
		int length = element.GetArrayLength();
		return index >= 0 && index < length ? element[index] : default;
	}

    /// <summary>
    /// Gets an array element at the specified index or default.
    /// </summary>
    internal static JsonElement GetElementAtOrDefault(this JsonElement element, Func<JsonElement, int> indexPredicate)
		=> element.GetElementAtOrDefault(indexPredicate(element));

    /// <summary>
    /// Converts a JSON number or string into a seconds-based TimeSpan.
    /// </summary>
    internal static TimeSpan? GetTimeSpanSeconds(this JsonElement element)
		=> TryGetInt32(element, out int seconds) ? TimeSpan.FromSeconds(seconds) : null;

    /// <summary>
    /// Parses a TimeSpan from "hh:mm:ss" or "mm:ss" format.
    /// </summary>
    internal static TimeSpan? GetTimeSpan(this JsonElement element)
	{
		string? str = element.ValueKind is JsonValueKind.String
			? element.GetString()
			: element.GetRawText();

		if (string.IsNullOrWhiteSpace(str))
		{
			return null;
		}

		// Using Span<Range> split skips manual index math entirely.
		Span<Range> parts = stackalloc Range[3];

		return str.Split(parts, ':') switch
		{
			2 when int.TryParse(str.AsSpan()[parts[0]], out int m)
			       && int.TryParse(str.AsSpan()[parts[1]], out int s)
				=> new TimeSpan(0, m, s),

			3 when int.TryParse(str.AsSpan()[parts[0]], out int h)
			       && int.TryParse(str.AsSpan()[parts[1]], out int m)
			       && int.TryParse(str.AsSpan()[parts[2]], out int s)
				=> new TimeSpan(h, m, s),

			_ => null
		};
	}

    /// <summary>
    /// Converts a JSON element to a primitive value type.
    /// </summary>
    private static T? ToValue<T>(this JsonElement element)
	{
		if (element.ValueKind is JsonValueKind.Undefined)
		{
			return default;
		}
		
		if (typeof(T) == typeof(string))
		{
			string? str = element.ValueKind is JsonValueKind.String
				? element.GetString()
				: element.GetRawText();

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
	{
		return element.ValueKind is JsonValueKind.Number
			? element.TryGetInt32(out value)
			: int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryGetInt64(JsonElement element, out long value)
	{
		return element.ValueKind is JsonValueKind.Number
			? element.TryGetInt64(out value)
			: long.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryGetDouble(this JsonElement element, out double value)
	{
		return element.ValueKind is JsonValueKind.Number
			? element.TryGetDouble(out value)
			: double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
	}
}
