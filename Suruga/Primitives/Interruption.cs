namespace Suruga.Primitives;

/// <summary>
/// Provides a cancellable token that can be signaled to interrupt ongoing operations.
/// </summary>
/// <remarks>
/// The token can be renewed, replacing the underlying source and resetting the
/// cancellation state.
/// </remarks>
internal class Interruption : IAsyncDisposable
{
	/// <summary>
	/// Gets the cancellation token that is signaled when an interruption occurs.
	/// </summary>
	internal CancellationToken Token => _cts.Token;

	private CancellationTokenSource _cts = new();

	/// <summary>
	/// Signals the interruption, canceling the current token.
	/// </summary>
	internal async ValueTask InterruptAsync()
	{
		if (!_cts.IsCancellationRequested)
		{
			await _cts.CancelAsync();
		}
	}

	/// <summary>
	/// Replaces the current token source with a new one, canceling the previous token.
	/// </summary>
	internal async Task RenewAsync()
	{
		CancellationTokenSource oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());

		try
		{
			await oldCts.CancelAsync();
		}
		catch
		{
			// No-op.
		}
		finally
		{
			oldCts.Dispose();
		}
	}

	public async ValueTask DisposeAsync()
	{
		CancellationTokenSource oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());

		try
		{
			await oldCts.CancelAsync();
		}
		catch
		{
			// No-op.
		}
		finally
		{
			oldCts.Dispose();
		}

		_cts.Dispose();
	}
}
