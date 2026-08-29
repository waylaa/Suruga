using System.Runtime.InteropServices;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFrame
{
    private IntPtrArray8 data;
    private fixed int linesize[8];
    public byte** extended_data;
    public int width;
    public int height;
    public int nb_samples;
    private int format;
    private int pict_type;
    private AVRational sample_aspect_ratio;
    private long pts;
    private long pkt_dts;
    private AVRational time_base;
    private int quality;
    private void* opaque;
    private int repeat_pict;
    private int sample_rate;
    private IntPtrArray8 buf;
    private nint extended_buf;
    private int nb_extended_buf;
    private nint side_data;
    private int nb_side_data;
    private int flags;
    private int color_range;
    private int color_primaries;
    private int color_trc;
    private int colorspace;
    private int chroma_location;
    private long best_effort_timestamp;
    private AVDictionary* metadata;
    private int decode_error_flags;
    private nint hw_frames_ctx;
    private nint opaque_ref;
    private nuint crop_top;
    private nuint crop_bottom;
    private nuint crop_left;
    private nuint crop_right;
    private void* private_ref;
    private AVChannelLayout ch_layout;
    private long duration;
    private int alpha_mode;
}
