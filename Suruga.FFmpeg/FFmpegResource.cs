using Suruga.FFmpeg.Helpers;

namespace Suruga.FFmpeg;

public abstract unsafe class FFmpegResource<T> : IDisposable where T : unmanaged
{
    internal T* Pointer
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return _pointer;
        }
        init
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _pointer = value;
        }
    }
    
    protected bool IsInitialized => _pointer is not null;
    
    protected bool IsDisposed;
    
    private T* _pointer;

    protected internal ref T GetReference()
        => ref PointerHelper.GetReference(_pointer);

    protected internal ref readonly T GetReadonlyReference()
        => ref PointerHelper.GetReadOnlyReference(_pointer);

    protected ref T* GetAddressOfPointer()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return ref _pointer;
    }
    
    protected void ThrowIfDisposedOrUninitialized()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        if (_pointer is null)
        {
            throw new InvalidOperationException("The native FFmpeg resource has not been initialized.");
        }
    }

    protected abstract void Release(ref T* addressOfPointer);

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        
        IsDisposed = true;
        
        Release(ref _pointer);
        GC.SuppressFinalize(this);
    }
}
