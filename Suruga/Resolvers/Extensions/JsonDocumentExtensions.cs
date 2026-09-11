using System.Text.Json;

namespace Suruga.Resolvers.Extensions;

internal static class JsonTraversalExtensions
{
    extension(JsonElement current)
    {
        internal JsonElement TraverseOrDefault(params ReadOnlySpan<string> path)
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

        internal JsonElement TraverseOrDefault(string property)
            => current.ValueKind is JsonValueKind.Object && current.TryGetProperty(property, out JsonElement value) ? value : default;

        internal T? TraverseValue<T>(params ReadOnlySpan<string> path)
            => current.TraverseOrDefault(path).ToValue<T>();

        internal T? TraverseValue<T>(string property)
            => current.ValueKind is JsonValueKind.Undefined ? default : current.TraverseOrDefault(property).ToValue<T>();

        internal JsonElement.ArrayEnumerator? EnumerateArrayOrNull()
            => current.ValueKind is JsonValueKind.Array ? current.EnumerateArray() : null;

        internal JsonElement GetElementAtOrDefault(int index)
        {
            if (current.ValueKind is not JsonValueKind.Array)
            {
                return default;
            }

            int length = current.GetArrayLength();
            return index >= 0 && index < length ? current[index] : default;
        }

        internal JsonElement GetElementAtOrDefault(Func<JsonElement, int> indexPredicate)
            => current.GetElementAtOrDefault(indexPredicate(current));
    }
}
