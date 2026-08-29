using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVDictionaryEntry
{
    public byte* key;
    public byte* value;
}
