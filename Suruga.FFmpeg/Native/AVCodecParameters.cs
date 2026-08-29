using System.Runtime.InteropServices;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodecParameters
{
    private AVMediaType codec_type;
    private int codec_id;
    private uint codec_tag;
    private byte* extradata;
    private int extradata_size;
    private nint coded_side_data;
    private int nb_coded_side_data;
    private int format;
    private long bit_rate;
    private int bits_per_coded_sample;
    private int bits_per_raw_sample;
    private int profile;
    private int level;
    private int width;
    private int height;
    private AVRational sample_aspect_ratio;
    private AVRational framerate;
    private int field_order;
    private int color_range;
    private int color_primaries;
    private int color_trc;
    private int color_space;
    private int chroma_location;
    private int video_delay;
    private AVChannelLayout ch_layout;
    private int sample_rate;
    private int block_align;
    private int frame_size;
    private int initial_padding;
    private int trailing_padding;
    private int seek_preroll;
    private int alpha_mode;
}
