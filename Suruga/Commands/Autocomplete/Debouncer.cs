using System.Collections.Concurrent;

namespace Suruga.Commands.Autocomplete;

/// <summary>
/// Prevents a function from being invoked too frequently by delaying execution and
/// cancelling pending calls when a new invocation occurs.
/// </summary>
/// <remarks>
/// Each operation is identified by a <typeparamref name="T"/> key. Calling
/// <see cref="WaitAsync(T)"/> with the same key cancels any pending wait for that key
/// before starting a new delay period.
/// </remarks>
internal sealed class Debouncer<T>(TimeSpan delay) where T : notnull
{
	private readonly ConcurrentDictionary<T, CancellationTokenSource> _ctsMap = [];

	/// <summary>
	/// Waits for the debounce period to elapse, canceling any existing wait
	/// for the same key.
	/// </summary>
	/// <param name="key">An identifier for the operation being debounced.</param>
	/// <returns>
	/// <see langword="true"/> if the delay completed without being
	/// canceled by a newer call for the same key. Otherwise, <see langword="false"/>
	/// </returns>
	internal async Task<bool> WaitAsync(T key)
	{
		if (_ctsMap.TryRemove(key, out CancellationTokenSource? oldCts))
		{
			await oldCts.CancelAsync();
			oldCts.Dispose();
		}

		CancellationTokenSource cts = new();
		_ctsMap[key] = cts;

		try
		{
			await Task.Delay(delay, cts.Token);
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		finally
		{
			if (_ctsMap.TryGetValue(key, out CancellationTokenSource? current) && ReferenceEquals(current, cts))
			{
				_ctsMap.TryRemove(key, out _);
			}

			cts.Dispose();
		}
	}
}
