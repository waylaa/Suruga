namespace Suruga.Audio.Primitives;

/// <summary>
/// Combines a swappable resource, a readiness signal, and an interruption token into
/// one primitive for "attach/detach a transport, interrupt and retry in-flight
/// operations on swap" scenarios.
/// </summary>
internal sealed class VolatileSwappableAsyncDisposableResource<T> : VolatileAsyncDisposable where T : class, IAsyncDisposable
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Func<T, ValueTask> _disposeValue;

    private T? _current;
    private CancellationTokenSource _interruptionCts = new();
    private TaskCompletionSource _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal VolatileSwappableAsyncDisposableResource(Func<T, ValueTask>? disposeValue = null) 
        => _disposeValue = disposeValue ?? (value => value.DisposeAsync());

    /// <summary>
    /// Gets the currently attached value without waiting, or <see langword="null"/>.
    /// </summary>
    /// <returns></returns>
    internal T? Peek()
        => Volatile.Read(ref _current);

    internal Task AttachAsync(T value, CancellationToken token = default)
    {
        ThrowIfDisposed();
        return SwapAsync(value, token);
    }

    internal Task DetachAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        return SwapAsync(null, token);
    }
    
    internal ValueTask UseAsync(Func<T, CancellationToken, ValueTask> operation, CancellationToken token = default)
    {
        ValueTask<bool> task = UseAsync(async (v, ct) =>
        {
            await operation(v, ct);
            return true;
        }, token);
        
        return new ValueTask(task.AsTask());
    }

    internal async ValueTask<TResult> UseAsync<TResult>(Func<T, CancellationToken, ValueTask<TResult>> operation, CancellationToken token = default)
    {
        ThrowIfDisposed();

        while (true)
        {
            // Snapshot the interrupt source alongside the ready-wait so a swap that
            // happens between those two reads is always visible as a fresh cancellation.
            CancellationTokenSource interruptCts = Volatile.Read(ref _interruptionCts);
            await _readySignal.Task.WaitAsync(token);
            
            T? current = Volatile.Read(ref _current);

            if (current is null)
            {
                continue;
            }

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, interruptCts.Token);

            try
            {
                return await operation(current, linkedCts.Token);
            }
            catch (OperationCanceledException) when (interruptCts.Token.IsCancellationRequested && !token.IsCancellationRequested)
            {
                // Swapped mid-operation, retry against the latest value.
            }
        }
    }
    
    protected override async ValueTask DisposeCoreAsync()
    {
        try
        {
            await SwapAsync(null, CancellationToken.None);
        }
        catch
        {
            // Ignore.
        }
        
        _interruptionCts.Dispose();
        _writeLock.Dispose();
    }

    private async Task SwapAsync(T? newValue, CancellationToken token)
    {
        await _writeLock.WaitAsync(token);

        try
        {
            // Close the gate and interrupt anyone mid-operation.
            _readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenSource oldInterruptCts =
                Interlocked.Exchange(ref _interruptionCts, new CancellationTokenSource());

            try
            {
                await oldInterruptCts.CancelAsync();
            }
            catch
            {
                // Ignore.
            }
            finally
            {
                oldInterruptCts.Dispose();
            }

            T? oldValue = Interlocked.Exchange(ref _current, newValue);

            if (oldValue is not null)
            {
                try
                {
                    await _disposeValue(oldValue);
                }
                catch
                {
                    // Ignore.
                }
            }

            // Re-open the gate only if a value is actually attached.
            if (newValue is not null)
            {
                _readySignal.TrySetResult();
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
