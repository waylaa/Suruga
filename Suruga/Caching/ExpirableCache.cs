using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace Suruga.Caching;

internal sealed class ExpirableCache<T>(IMemoryCache cache)
{
    internal bool TryGet(string key, [NotNullWhen(true)] out T? value)
        => cache.TryGetValue(key, out value);

    internal void Set(string key, T value, TimeSpan expiration)
        => cache.Set(key, value, expiration);
}
