namespace Suruga.Primitives;

/// <summary>
/// Provides a lightweight, asynchronous signaling mechanism similar to a manual-reset event.
/// </summary>
internal sealed class Signal
{
    /// <summary>
    /// The underlying task completion source used to manage the current signal state.
    /// </summary>
    private volatile TaskCompletionSource _waitHandle = CreateSignaledHandle();

    /// <summary>
    /// Asynchronously waits for the signal to be set.
    /// </summary>
    /// <param name="onWait">
    /// An optional asynchronous delegate invoked continuously while the signal remains unsignaled.
    /// </param>
    /// <param name="token">A cancellation token that can be used to cancel the wait operation.</param>
    /// <returns>A task that completes when the signal is set or the operation is canceled.</returns>
    /// <exception cref="OperationCanceledException">
    /// The <paramref name="token"/> was canceled before the signal was set.
    /// </exception>
    internal async Task WaitAsync(Func<CancellationToken, Task>? onWait = null, CancellationToken token = default)
	{
		while (true)
		{
			TaskCompletionSource currentHandle = _waitHandle;

			if (!currentHandle.Task.IsCompleted && onWait is not null)
			{
				await onWait(token);
			}

			await currentHandle.Task.WaitAsync(token);

			// Reset() may have swapped the handle after we read a signaled one.
			TaskCompletionSource latestHandle = _waitHandle;

			if (latestHandle == currentHandle || latestHandle.Task.IsCompleted)
			{
				return;
			}
		}
	}

    /// <summary>
    /// Sets the state of the signal to signaled, releasing all waiting tasks.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the signal was successfully changed to the signaled state; 
    /// <c>false</c> if it was already signaled.
    /// </returns>
    internal bool Set()
	{
		while (true)
		{
			TaskCompletionSource currentHandle = _waitHandle;

			// Already open.
			if (currentHandle.Task.IsCompleted)
			{
				return false;
			}

			if (Interlocked.CompareExchange(ref _waitHandle, CreateSignaledHandle(), currentHandle) == currentHandle)
			{
				currentHandle.TrySetResult();
				return true;
			}
		}
	}

    /// <summary>
    /// Sets the state of the signal to unsignaled, causing subsequent calls to 
    /// <see cref="WaitAsync"/> to wait until <see cref="Set"/> is called.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the signal was successfully changed to the unsignaled state; 
    /// <c>false</c> if it was already unsignaled.
    /// </returns>
    internal bool Reset()
	{
		while (true)
		{
			TaskCompletionSource currentHandle = _waitHandle;

			// Already closed.
			if (!currentHandle.Task.IsCompleted)
			{
				return false;
			}

			TaskCompletionSource closedHandle = CreateUnsignaledHandle();

			if (Interlocked.CompareExchange(ref _waitHandle, closedHandle, currentHandle) == currentHandle)
			{
				return true;
			}
		}
	}

    /// <summary>
    /// Creates a new <see cref="TaskCompletionSource"/> that is already in a completed (signaled) state.
    /// </summary>
    /// <returns>A completed <see cref="TaskCompletionSource"/>.</returns>
    private static TaskCompletionSource CreateSignaledHandle()
	{
		TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
		tcs.TrySetResult();

		return tcs;
	}

    /// <summary>
    /// Creates a new <see cref="TaskCompletionSource"/> that is in an incomplete (unsignaled) state.
    /// </summary>
    /// <returns>An incomplete <see cref="TaskCompletionSource"/>.</returns>
    private static TaskCompletionSource CreateUnsignaledHandle()
		=> new(TaskCreationOptions.RunContinuationsAsynchronously);
}
