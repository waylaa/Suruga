using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVStream
{
    private nint av_class;
    private int index;
    private int id;
    public AVCodecParameters* codecpar;
    private void* priv_data;
    public AVRational time_base;
    private long start_time;
    public long duration;
    private long nb_frames;
    private int disposition;
    private int discard;
    private AVRational sample_aspect_ratio;
    public AVDictionary* metadata;
    private AVRational avg_frame_rate;
    private AVPacket attached_pic;
    private int event_flags;
    private AVRational r_frame_rate;
    private int pts_wrap_bits;
}
