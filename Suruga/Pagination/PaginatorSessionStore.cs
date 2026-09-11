using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NetCord.Rest;

namespace Suruga.Pagination;

/// <summary>
/// Stores active paginator sessions keyed by guild, with no expiration policy of its own.
/// </summary>
internal sealed class PaginatorSessionStore
{
    private readonly ConcurrentDictionary<ulong, (RestMessage Message, IPaginatorSession Session)> _sessions = [];

    internal void Set(ulong guildId, RestMessage message, IPaginatorSession session)
        => _sessions[guildId] = (message, session);

    internal bool TryGet<T>(ulong guildId, [NotNullWhen(true)] out PaginatorSession<T>? state)
    {
        if (_sessions.TryGetValue(guildId, out (RestMessage Message, IPaginatorSession Session) tuple) &&
            tuple.Session is PaginatorSession<T> typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }

    internal async ValueTask<bool> TryRemoveAsync(ulong guildId)
    {
        if (!_sessions.TryRemove(guildId, out (RestMessage Message, IPaginatorSession Session) tuple))
        {
            return false;
        }

        await tuple.Session.DisposeAsync();
        return true;
    }
}
