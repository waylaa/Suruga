using Suruga.Audio.Primitives;

namespace Suruga.Primitives;

/// <summary>
/// Provides an asynchronous mutual exclusion lock with scoped release semantics.
/// </summary>
internal sealed class AsyncMutex : VolatileDisposable
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	/// <summary>
	/// Asynchronously acquires the lock and returns a disposable scope that releases it.
	/// </summary>
	internal async Task<Scope> EnterScopeAsync(CancellationToken token = default)
	{
		ThrowIfDisposed();
		await _semaphore.WaitAsync(token);

		if (IsDisposed)
		{
			TryRelease(_semaphore);
			throw new ObjectDisposedException(nameof(AsyncMutex));
		}
		
		return new Scope(_semaphore);
	}

	protected override void DisposeCore()
		=> _semaphore.Dispose();

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
    /// A scope that releases the <see cref="AsyncMutex"/> when disposed.
    /// </summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        internal Scope(SemaphoreSlim semaphore)
            => _semaphore = semaphore;

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public void Dispose()
            => TryRelease(_semaphore);
    }
}
