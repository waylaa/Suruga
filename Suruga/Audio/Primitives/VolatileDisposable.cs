namespace Suruga.Audio.Primitives;

internal abstract class VolatileDisposable : IDisposable
{
    protected bool IsDisposed => Volatile.Read(ref _state) != 0;

    private int _state;

    protected void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(IsDisposed, this);

    protected abstract void DisposeCore();
    
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }
        
        DisposeCore();
    }
}
