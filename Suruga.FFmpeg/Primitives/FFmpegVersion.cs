using System.Runtime.InteropServices;
using System.Text;
using Suruga.FFmpeg.Interop;

namespace Suruga.FFmpeg.Primitives;

public static unsafe class FFmpegVersion
{
    public static string Version
    {
        get
        {
            ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(NativeMethods.av_version_info());
            return Encoding.UTF8.GetString(span);
        }
    }
}
