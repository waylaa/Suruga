using Suruga.FFmpeg.Extensions;
using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg;

public sealed unsafe class ResamplerContext : FFmpegResource<SwrContext>
{
    private readonly int _inputSampleRate;
    
    private AVChannelLayout _inputLayout;
    private AVChannelLayout _outputLayout;
    
    private const int OutputSampleRate = 48000;
    
    public ResamplerContext(CodecContext codecContext)
    {
        _inputSampleRate = codecContext.SampleRate;
        
        codecContext.GetChannelLayout(out _inputLayout);
        NativeMethods.av_channel_layout_default(ref _outputLayout, 2);

        NativeMethods.swr_alloc_set_opts2
        (
            ref GetAddressOfPointer(),
            ref _outputLayout,
            AVSampleFormat.AV_SAMPLE_FMT_FLT,
            OutputSampleRate,
            ref _inputLayout,
            codecContext.SampleFormat,
            _inputSampleRate,
            0,
            null
        ).ThrowIfError();
        
        NativeMethods.swr_init(ref GetReference()).ThrowIfError();
    }

    public AudioChunk Resample(Frame source)
    {
        long delay = NativeMethods.swr_get_delay(ref GetReference(), _inputSampleRate) + source.SamplesPerChannel;
        int maxOutputSamples = (int)NativeMethods.av_rescale_rnd(delay, OutputSampleRate, _inputSampleRate, AVRounding.AV_ROUND_UP);
        
        AudioChunk chunk = new(maxOutputSamples * _outputLayout.nb_channels);
        
        fixed (byte* pChunkBuffer = chunk.Buffer.Span)
        {
            try
            {
                int samplesWritten = NativeMethods.swr_convert
                (
                    ref GetReference(),
                    in pChunkBuffer,
                    maxOutputSamples,
                    in source.ExtendedData,
                    source.SamplesPerChannel
                ).ThrowIfError();

                chunk.Resize(samplesWritten * _outputLayout.nb_channels);
            }
            catch
            {
                chunk.Dispose();
            }
        }

        return chunk;
    }

    protected override void Release(ref SwrContext* addressOfPointer)
    {
        NativeMethods.av_channel_layout_uninit(ref _inputLayout);
        NativeMethods.av_channel_layout_uninit(ref _outputLayout);
        
        NativeMethods.swr_free(ref addressOfPointer);
    }
}
