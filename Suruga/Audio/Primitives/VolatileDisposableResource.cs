namespace Suruga.Audio.Primitives;

/// <summary>
/// Holds a single disposable value and guarantees the previous value is torn down
/// whenever it is replaced or cleared.
/// </summary>
internal sealed class VolatileDisposableResource<T> where T : class, IDisposable
{
	internal T? Current => Volatile.Read(ref _value);

	private T? _value;

	/// <summary>
	/// Atomically replaces the stored value and disposes the previous one, if any.
	/// </summary>
	internal void Replace(T? newValue)
	{
		T? oldValue = Interlocked.Exchange(ref _value, newValue);
		oldValue?.Dispose();
	}

	internal void Clear()
		=> Replace(null);
}
