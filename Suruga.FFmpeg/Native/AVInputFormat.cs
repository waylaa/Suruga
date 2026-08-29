using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AVInputFormat
{
    private byte* name;
    private byte* long_name;
    private int flags;
    private byte* extensions;
    private nint codec_tag;
    private nint priv_class;
    private byte* mime_type;
}
