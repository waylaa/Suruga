namespace Suruga.Audio.FFmpeg.Abstractions;

internal abstract unsafe class FFmpegResourceBase<T>() : IDisposable where T : unmanaged
{
    protected bool IsAlive => Pointer is not null && !IsDisposed;

    internal T* Pointer;

    protected bool IsDisposed;

    /// <summary>
    /// Releases the corresponding FFmpeg resource.
    /// </summary>
    protected virtual void Release()
    {
    }

    public void Dispose()
    {
        if (!IsAlive)
        {
            return;
        }

        Release();

        IsDisposed = true;
        Pointer = null;
    }
}
