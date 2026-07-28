namespace Suruga.Audio.Primitives;

/// <summary>
/// Tracks a single in-flight cancellable operation (a linked <see cref="CancellationTokenSource"/>
/// paired with the <see cref="Task"/> it drives) and provides safe start/stop semantics.
/// </summary>
internal sealed class CancellableAsyncWork
{
    private CancellationTokenSource? _cts;
    private Task? _task;

    /// <summary>
    /// Starts new work, first stopping any previously running work.
    /// </summary>
    internal async Task StartAsync(Func<CancellationToken, Task> work, CancellationToken token = default)
    {
        await StopAsync();

        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _cts = cts;
        _task = work(cts.Token);
    }

    /// <summary>
    /// Cancels and awaits the current work, if any, swallowing cancellation/faults.
    /// Safe to call even if no work is running.
    /// </summary>
    internal async Task StopAsync()
    {
        CancellationTokenSource? currentCts = Interlocked.Exchange(ref _cts, null);

        if (currentCts is not null)
        {
            try
            {
                await currentCts.CancelAsync();
            }
            catch
            {
                // Ignore.
            }
            finally
            {
                currentCts.Dispose();
            }
        }

        Task? currentTask = Interlocked.Exchange(ref _task, null);

        if (currentTask is not null)
        {
            try
            {
                await currentTask;
            }
            catch
            {
                // Ignore.
            }
        }
    }
}
