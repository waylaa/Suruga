using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AVIOInterruptCB
{
    private delegate* unmanaged[Cdecl]<void*, int> callback;
    private void* opaque;
}
