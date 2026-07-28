namespace Suruga.Audio.Primitives;

internal abstract class VolatileAsyncDisposable : IAsyncDisposable
{
    protected bool IsDisposed => Volatile.Read(ref _state) != 0;

    private int _state;

    protected void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(IsDisposed, this);

    protected abstract ValueTask DisposeCoreAsync();
    
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }
        
        await DisposeCoreAsync();
    }
}
