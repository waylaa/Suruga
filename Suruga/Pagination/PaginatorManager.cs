using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NetCord.Rest;

namespace Suruga.Pagination;

/// <summary>
/// Manages active paginator sessions and automatically expires them.
/// </summary>
/// <param name="expirationFromNow">
/// The session lifetime before automatic expiration. Defaults to two minutes.
/// </param>
internal sealed class PaginatorManager(TimeSpan? expirationFromNow = null)
{
	private readonly ConcurrentDictionary<ulong, (RestMessage, IPaginatorSession)> _sessions = [];
	private readonly TimeSpan _expirationFromNow = expirationFromNow ?? TimeSpan.FromMinutes(2);

    /// <summary>
    /// Registers a paginator session for a guild.
    /// </summary>
    /// <typeparam name="T">The item type being paginated.</typeparam>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="message">The message associated with the session.</param>
    /// <param name="session">The paginator session.</param>
    /// <param name="onExpiration">
    /// An optional callback invoked when the session expires.
    /// </param>
    internal void Add<T>(ulong guildId, RestMessage message, PaginatorSession<T> session, Func<RestMessage, Task>? onExpiration = null)
	{
		_sessions[guildId] = (message, session);
		_ = ExpireAsync(guildId, message, session, onExpiration);
	}

    /// <summary>
    /// Attempts to retrieve a paginator session for a guild.
    /// </summary>
    /// <typeparam name="T">The item type being paginated.</typeparam>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="state">The retrieved session, if found.</param>
    /// <returns>
    /// <see langword="true"/> if a matching session was found; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal bool TryGet<T>(ulong guildId, [NotNullWhen(true)] out PaginatorSession<T>? state)
	{
		if (_sessions.TryGetValue(guildId, out (RestMessage, IPaginatorSession s) tuple) && tuple.s is PaginatorSession<T> typed)
		{
			state = typed;
			return true;
		}

		state = null;
		return false;
	}

	private async Task ExpireAsync<T>(ulong guildId, RestMessage message, PaginatorSession<T> session, Func<RestMessage, Task>? onExpiration = null)
	{
		try
		{
			await Task.Delay(_expirationFromNow, session.CancellationToken);
		}
		catch (TaskCanceledException)
		{
			return;
		}

		try
		{
			if (onExpiration is not null)
			{
				await onExpiration(message);
			}
		}
		finally
		{
			if (_sessions.TryRemove(guildId, out (RestMessage, IPaginatorSession s) tuple))
			{
				await tuple.s.DisposeAsync();
			}
		}
	}
}
