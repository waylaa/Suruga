using System.Globalization;
using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using Suruga.Audio.FFmpeg.Extensions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class FilterGraph : FFmpegResourceBase<AVFilterGraph>
{
    private readonly AVFilterContext* _abufferPtr;
    private readonly AVFilterContext* _abuffersinkPtr;

    private const int AvBuffersrcFlagKeepRef = 1;

    internal FilterGraph
    (
        AVChannelLayout inputLayout, AVSampleFormat inputFormat, int inputSampleRate,
        AVChannelLayout outputLayout, AVSampleFormat outputFormat, int outputSampleRate
    )
    {
        Pointer = avfilter_graph_alloc();

        if (Pointer is null)
        {
            throw new OutOfMemoryException();
        }

        fixed (AVFilterContext** abufferAddress = &_abufferPtr)
        {
            avfilter_graph_create_filter
            (
                filt_ctx: abufferAddress,
                filt: avfilter_get_by_name("abuffer"),
                name: "BufferInput",
                args: $"sample_rate={inputSampleRate}:" +
                      $"sample_fmt={av_get_sample_fmt_name(inputFormat)}:" +
                      $"channel_layout=0x{inputLayout.u.mask:X}",
                opaque: null,
                graph_ctx: Pointer
            ).ThrowOnError();
        }

        AVFilterContext* atempoPtr;
        avfilter_graph_create_filter
        (
            filt_ctx: &atempoPtr,
            filt: avfilter_get_by_name("atempo"),
            name: "SpeedFilter",
            args: null,
            opaque: null,
            graph_ctx: Pointer
        ).ThrowOnError();

        AVFilterContext* aformatPtr;
        avfilter_graph_create_filter
        (
            filt_ctx: &aformatPtr,
            filt: avfilter_get_by_name("aformat"),
            name: "FormatFilter",
            args: $"sample_fmts={av_get_sample_fmt_name(outputFormat)}:" +
                  $"channel_layouts=0x{outputLayout.u.mask:X}:" +
                  $"sample_rates={outputSampleRate}",
            opaque: null,
            graph_ctx: Pointer
        ).ThrowOnError();

        fixed (AVFilterContext** abufferSinkAddress = &_abuffersinkPtr)
        {
            avfilter_graph_create_filter
            (
                filt_ctx: abufferSinkAddress,
                filt: avfilter_get_by_name("abuffersink"),
                name: "BufferOutput",
                args: null,
                opaque: null,
                graph_ctx: Pointer
            ).ThrowOnError();
        }

        avfilter_link(_abufferPtr, 0, atempoPtr, 0);
        avfilter_link(atempoPtr, 0, aformatPtr, 0);
        avfilter_link(aformatPtr, 0, _abuffersinkPtr, 0);

        avfilter_graph_config(Pointer, log_ctx: null);
    }

    internal void Write(Frame inputFrame)
        => av_buffersrc_add_frame_flags(_abufferPtr, inputFrame.Pointer, AvBuffersrcFlagKeepRef).ThrowOnError();

    internal FFmpegStatus Read(out DecodedFrame<float>? decodedFrame)
    {
        using Frame outputFrame = new();

        int rc = av_buffersink_get_frame(_abuffersinkPtr, outputFrame.Pointer);

        if (rc == AVERROR_EOF)
        {
            decodedFrame = null;
            outputFrame.Unreference();

            return FFmpegStatus.EndOfStream;
        }

        if (rc == AVERROR(EAGAIN))
        {
            decodedFrame = null;
            outputFrame.Unreference();
            return FFmpegStatus.NeedMoreInput;
        }

        rc.ThrowOnError();

        decodedFrame = outputFrame.ToDecodedFrame();
        return FFmpegStatus.Success;
    }

    internal void Flush()
        => av_buffersrc_close(_abufferPtr, pts: AV_NOPTS_VALUE, flags: 0).ThrowOnError();

    internal void SetSpeed(float value)
        => SendCommand("SpeedFilter", "tempo", value.ToString(CultureInfo.InvariantCulture));

    protected override void Release()
    {
        fixed (AVFilterGraph** address = &Pointer)
        {
            // Filter contexts are freed automatically.
            avfilter_graph_free(address);
        }
    }

    private void SendCommand(string filterInstance, string property, string value)
        => avfilter_graph_send_command(Pointer, filterInstance, property, value, res: null, res_len: 0, flags: 0).ThrowOnError();
}
