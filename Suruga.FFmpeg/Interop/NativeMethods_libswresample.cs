using System.Runtime.InteropServices;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg.Interop;

internal sealed unsafe partial class NativeMethods
{
    [LibraryImport(FFmpegLibraryNames.SwResample)]
    internal static partial int swr_alloc_set_opts2
    (
        ref SwrContext* ps,
        ref readonly AVChannelLayout out_ch_layout,
        AVSampleFormat out_sample_fmt,
        int out_sample_rate,
        ref readonly AVChannelLayout in_ch_layout,
        AVSampleFormat in_sample_fmt,
        int in_sample_rate,
        int log_offset,
        void* log_ctx
    );

    [LibraryImport(FFmpegLibraryNames.SwResample)]
    internal static partial int swr_init(ref SwrContext s);

    [LibraryImport(FFmpegLibraryNames.SwResample)]
    internal static partial int swr_convert
    (
        ref SwrContext s,
        ref readonly byte* @out,
        int out_count,
        ref readonly byte* @in,
        int in_count
    );

    [LibraryImport(FFmpegLibraryNames.SwResample)]
    internal static partial long swr_get_delay(ref SwrContext s, long @base);
    
    [LibraryImport(FFmpegLibraryNames.SwResample)]
    internal static partial void swr_free(ref SwrContext* s);
}
