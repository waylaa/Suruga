using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Primitives;

[StructLayout(LayoutKind.Sequential)]
[InlineArray(8)]
internal struct IntPtrArray8
{
    private nint _element0;
}
