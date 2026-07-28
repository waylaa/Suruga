namespace Suruga.Audio.Primitives;

/// <summary>
/// Holds a single disposable value and guarantees the previous value is torn down
/// whenever it is replaced or cleared.
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class VolatileAsyncDisposableResource<T> where T : class, IAsyncDisposable
{
    internal T? Current => Volatile.Read(ref _value);
    
    private T? _value;

    /// <summary>
    /// Atomically replaces the stored value and disposes the previous one, if any.
    /// </summary>
    /// <param name="newValue"></param>
    /// <returns></returns>
    internal async Task ReplaceAsync(T? newValue)
    {
        T? oldValue = Interlocked.Exchange(ref _value, newValue);

        if (oldValue is not null)
        {
            try
            {
                await oldValue.DisposeAsync();
            }
            catch
            {
                // Ignore disposal exceptions to the old value.
            }
        }
    }

    internal Task ClearAsync()
        => ReplaceAsync(null);
}
