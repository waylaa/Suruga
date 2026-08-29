using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVPacket
{
    private nint buf;
    private long pts;
    private long dts;
    private byte* data;
    private int size;
    public int stream_index;
    private int flags;
    private nint side_data;
    private int side_data_elems;
    private long duration;
    private long pos;
    private void* opaque;
    private nint opaque_ref;
    private AVRational time_base;
}
