using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodec
{
    private byte* name;
    private byte* long_name;
    private int type;
    private int id;
    private int capabilities;
    private byte max_lowres;
    private nint priv_class;
    private nint profiles;
    private byte* wrapper_name;
}
