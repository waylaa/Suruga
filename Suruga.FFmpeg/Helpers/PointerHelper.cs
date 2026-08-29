namespace Suruga.FFmpeg.Helpers;

internal static unsafe class PointerHelper
{
    internal static ref T GetReference<T>(T* pointer) where T : unmanaged
        => ref *pointer;
    
    internal static ref readonly T GetReadOnlyReference<T>(T* pointer) where T : unmanaged
        => ref *pointer;
}
