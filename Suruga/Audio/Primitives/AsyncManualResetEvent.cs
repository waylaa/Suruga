namespace Suruga.Audio.Primitives;

/// <summary>
/// A lightweight, resettable async gate.
/// Waiters block while closed and are released when <see cref="Set"/> is called.
/// </summary>
internal sealed class AsyncManualResetEvent
{
    internal bool IsOpen => _tcs.Task.IsCompleted;
    
    private volatile TaskCompletionSource _tcs = CreateOpen();

    /// <summary>
    /// Waits for the gate to open. If <paramref name="onWait"/> is provided, it is
    /// invoked once before waiting whenever the gate is currently closed (useful for
    /// running side effects (e.g. flushing)) right before blocking.
    /// </summary>
    internal async Task WaitAsync(Func<CancellationToken, Task>? onWait = null, CancellationToken token = default)
    {
        TaskCompletionSource tcs = _tcs;

        if (!tcs.Task.IsCompleted && onWait is not null)
        {
            await onWait(token);
        }

        await tcs.Task.WaitAsync(token);
    }
    
    /// <summary>
    /// Opens the gate, releasing all current and future waiters until <see cref="Reset"/>.
    /// </summary>
    internal void Set()
        => _tcs.TrySetResult();

    /// <summary>
    /// Closes the gate, causing subsequent <see cref="WaitAsync"/> calls to block.
    /// </summary>
    internal void Reset()
    {
        if (_tcs.Task.IsCompleted)
        {
            _tcs = CreateClosed();
        }
    }

    private static TaskCompletionSource CreateOpen()
    {
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.TrySetResult();

        return tcs;
    }
    
    private static TaskCompletionSource CreateClosed()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
