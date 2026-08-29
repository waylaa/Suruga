using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public struct AVRational
{
    public int num;
    public int den;
}
