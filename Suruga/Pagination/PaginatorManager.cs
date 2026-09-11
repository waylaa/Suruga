using System.Diagnostics.CodeAnalysis;
using NetCord.Rest;

namespace Suruga.Pagination;

internal sealed class PaginatorManager(TimeSpan? expirationFromNow = null)
{
    private readonly PaginatorSessionStore _store = new();
    private readonly PaginatorExpirationScheduler _scheduler = new(expirationFromNow ?? TimeSpan.FromMinutes(2));

    internal void Add<T>(ulong guildId, RestMessage message, PaginatorSession<T> session, Func<RestMessage, Task>? onExpiration = null)
    {
        _store.Set(guildId, message, session);

        _scheduler.Schedule(session, async () =>
        {
            try
            {
                if (onExpiration is not null)
                {
                    await onExpiration(message);
                }
            }
            finally
            {
                await _store.TryRemoveAsync(guildId);
            }
        });
    }

    internal bool TryGet<T>(ulong guildId, [NotNullWhen(true)] out PaginatorSession<T>? state)
        => _store.TryGet(guildId, out state);
}
