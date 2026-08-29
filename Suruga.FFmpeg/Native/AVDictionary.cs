using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public struct AVDictionary
{
    public int count;
    public nint elems;
}
