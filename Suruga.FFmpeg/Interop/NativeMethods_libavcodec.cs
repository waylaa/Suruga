using System.Runtime.InteropServices;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg.Interop;

internal sealed unsafe partial class NativeMethods
{
    #region AVCodecContext
    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial AVCodecContext* avcodec_alloc_context3(ref readonly AVCodec codec);

    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial int avcodec_parameters_to_context(ref AVCodecContext codec, ref readonly AVCodecParameters par);

    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial int avcodec_open2(ref AVCodecContext avctx, ref readonly AVCodec codec, ref AVDictionary* options);

    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial int avcodec_send_packet(ref AVCodecContext avctx, ref readonly AVPacket avpkt);
    
    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial int avcodec_receive_frame(ref AVCodecContext avctx, ref AVFrame frame);
    
    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial void avcodec_flush_buffers(ref AVCodecContext avctx);
    
    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial void avcodec_free_context(ref AVCodecContext* avctx);
    #endregion
    
    #region AVPacket
    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial AVPacket* av_packet_alloc();

    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial void av_packet_unref(ref AVPacket pkt);

    [LibraryImport(FFmpegLibraryNames.AvCodec)]
    internal static partial void av_packet_free(ref AVPacket* pkt);
    #endregion
}
