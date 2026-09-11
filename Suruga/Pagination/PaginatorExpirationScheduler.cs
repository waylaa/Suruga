namespace Suruga.Pagination;

/// <summary>
/// Schedules a callback to run once a paginator session's cancellation token
/// has not fired within a fixed window.
/// </summary>
internal sealed class PaginatorExpirationScheduler(TimeSpan expirationFromNow)
{
    internal void Schedule(IPaginatorSession session, Func<Task> onExpired)
        => _ = RunAsync(session, onExpired);

    private async Task RunAsync(IPaginatorSession session, Func<Task> onExpired)
    {
        try
        {
            await Task.Delay(expirationFromNow, session.CancellationToken);
        }
        catch (TaskCanceledException)
        {
            return; // Session was disposed or replaced before expiring.
        }

        await onExpired();
    }
}
