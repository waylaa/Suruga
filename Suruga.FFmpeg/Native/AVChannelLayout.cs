using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVChannelLayout
{
    private int order;
    public int nb_channels;
    private nint u;
    private void* opaque;
}
