using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Suruga.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="Enum"/> values.
/// </summary>
internal static class EnumExtensions
{
    private static readonly ConcurrentDictionary<(Type EnumType, string Name), string> _descriptionCache = new();

    /// <summary>
    /// Gets the description of an enum value defined by <see cref="DescriptionAttribute"/>,
    /// or falls back to the enum value name if no description exists.
    /// </summary>
    /// <param name="value">The enum value.</param>
    /// <returns>The description or the enum name.</returns>
    internal static string GetDescription(this Enum value)
	{
        Type type = value.GetType();
        string name = value.ToString();

        return _descriptionCache.GetOrAdd((type, name), static key =>
        {
            (Type enumType, string enumName) = key;
            FieldInfo? field = enumType.GetField(enumName);

            if (field is null)
            {
                return enumName;
            }

            DescriptionAttribute? attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? enumName;
        });
	}
}
