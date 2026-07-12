using System.Runtime.ConstrainedExecution;

namespace Suruga.Audio.Decode;

/// <summary>
/// A base class for managing FFmpeg native resources.
/// </summary>
/// <typeparam name="T">An unmanaged type representing the FFmpeg resource.</typeparam>
internal abstract unsafe class FFmpegResource<T> : CriticalFinalizerObject, IDisposable where T : unmanaged
{
    /// <summary>
    /// Gets a value indicating whether the resource is allocated and not yet disposed.
    /// </summary>
    protected bool IsAlive => Pointer is not null && !_isDisposed;

    /// <summary>
    /// Gets a value indicating whether the pointer is not null, regardless of
    /// disposal state.
    /// </summary>
    protected bool IsValid => Pointer is not null;

    /// <summary>
    /// The raw pointer to the FFmpeg resource.
    /// </summary>
    /// <remarks>This field is intended for derived class use.</remarks>
    internal T* Pointer;

    private bool _isDisposed;

    /// <summary>
    /// Finalizes an instance of the <see cref="FFmpegResource{T}"/> class.
    /// </summary>
    /// <remarks>
    /// The finalizer calls <see cref="Dispose()"/> to release the native resource
    /// if it hasn't been already released.
    /// </remarks>
    ~FFmpegResource()
        => ReleaseCore();

    /// <summary>
    /// Releases the underlying FFmpeg resource.
    /// </summary>
    protected abstract void Release();

    public void Dispose()
    {
        ReleaseCore();
        GC.SuppressFinalize(this);
    }

    private void ReleaseCore()
    {
        if (_isDisposed)
        {
            return;
        }
        
        if (Pointer is not null)
        {
            Release();
            Pointer = null;
        }

        _isDisposed = true;
    }
}
