namespace Suruga.Primitives;

/// <summary>
/// Provides an asynchronous mutual exclusion lock with scoped release semantics.
/// </summary>
/// <remarks>
/// Use <see cref="EnterScopeAsync"/> to acquire the lock asynchronously and release it
/// by disposing the returned scope.
/// </remarks>
internal sealed class AsyncLock : IDisposable
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	private bool _isDisposed;

	/// <summary>
	/// Asynchronously acquires the lock and returns a disposable scope that releases it.
	/// </summary>
	/// <param name="token"> A cancellation token to cancel the wait operation.</param>
	/// <returns>
	/// A task that completes with an <see cref="AsyncScope"/> when the lock is acquired.
	/// </returns>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if this <see cref="AsyncLock"/> has already been disposed.
	/// </exception>
	internal async Task<AsyncScope> EnterScopeAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		
		await _semaphore.WaitAsync(token);

		if (_isDisposed)
		{
			TryRelease(_semaphore);
			throw new ObjectDisposedException(nameof(AsyncLock));
		}

		return new AsyncScope(_semaphore);
	}
	
	/// <summary>
	/// Disposes the underlying semaphore.
	/// </summary>
	public void Dispose()
	{
		_isDisposed = true;
		_semaphore.Dispose();
	}

	private static void TryRelease(SemaphoreSlim semaphore)
	{
		try
		{
			semaphore.Release();
		}
		catch (ObjectDisposedException)
		{
			// A waiter can be released after the lock is disposed during shutdown.
		}
	}

    /// <summary>
    /// A scope that releases the <see cref="AsyncLock"/> when disposed.
    /// </summary>
    internal readonly struct AsyncScope : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        internal AsyncScope(SemaphoreSlim semaphore)
            => _semaphore = semaphore;

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public void Dispose()
            => TryRelease(_semaphore);
    }
}
