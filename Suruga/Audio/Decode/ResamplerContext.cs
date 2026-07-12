using FFmpeg.AutoGen;
using Suruga.Audio.Decode.Primitives;
using Suruga.Extensions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="SwrContext"/> used to convert decoded audio to another format.
/// </summary>
internal sealed unsafe class ResamplerContext : FFmpegResource<SwrContext>
{
    private readonly int _inputSampleRate;
    private readonly AVChannelLayout _outputLayout;
    
    private const int OutputSampleRate = 48000;

    /// <summary>
    /// Initializes a resampler for the specified input audio format.
    /// </summary>
    /// <param name="inputLayout">The input channel layout.</param>
    /// <param name="inputFormat">The input sample format.</param>
    /// <param name="inputSampleRate">The input sample rate.</param>
    internal ResamplerContext(AVChannelLayout inputLayout, AVSampleFormat inputFormat, int inputSampleRate)
    {
        _inputSampleRate = inputSampleRate;

        fixed (SwrContext** ppSwrContext = &Pointer)
        fixed (AVChannelLayout* pOutputLayout = &_outputLayout)
        {
            av_channel_layout_default(pOutputLayout, 2); // Stereo output.

            swr_alloc_set_opts2
            (
                ppSwrContext,
                pOutputLayout,
                AVSampleFormat.AV_SAMPLE_FMT_FLT,
                48000,
                &inputLayout,
                inputFormat,
                inputSampleRate,
                0,
                null

            ).ThrowOnError(rc => rc < 0);
        }

        swr_init(Pointer).ThrowOnError(rc => rc < 0);
    }
    
    /// <summary>
    /// Converts a decoded audio frame to 48 kHz stereo floating-point audio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the source frame already matches the target format, the returned
    /// buffer references the source frame data directly and no resampling
    /// occurs. Otherwise, a new managed buffer is allocated and populated with the
    /// resampled audio data.
    /// </para>
    /// </remarks>
    /// <param name="sourceFrame">The source frame to resample.</param>
    /// <returns>
    /// An audio buffer containing the converted audio samples.
    /// </returns>
    internal IAudioFrameBuffer Resample(Frame sourceFrame)
    {
        if (sourceFrame.Pointer->format == (int)AVSampleFormat.AV_SAMPLE_FMT_FLT &&
            sourceFrame.Pointer->sample_rate == 48000 &&
            sourceFrame.Pointer->ch_layout.nb_channels == 2)
        {
            return new UnmanagedAudioFrameBuffer(sourceFrame);
        }
        
        long delay = swr_get_delay(Pointer, _inputSampleRate) + sourceFrame.SampleCountPerChannel;
        int maxOutputSamples = (int)av_rescale_rnd(delay, OutputSampleRate, _inputSampleRate, AVRounding.AV_ROUND_UP);
        ManagedAudioFrameBuffer outputFrame = new(maxOutputSamples * _outputLayout.nb_channels);

        fixed (byte* pOutputFrameBuffer = outputFrame.Buffer.Span)
        {
            byte** ppAudioPlanes = stackalloc byte*[1] { pOutputFrameBuffer };
            
            int actualSamplesWritten = swr_convert
            (
                Pointer,
                ppAudioPlanes,
                maxOutputSamples,
                sourceFrame.ExtendedData,
                sourceFrame.SampleCountPerChannel
            ).ThrowOnError(rc => rc < 0, outputFrame.Dispose);

            outputFrame.Resize(actualSamplesWritten * _outputLayout.nb_channels);
        }

        return outputFrame;
    }
    
    protected override void Release()
    {
        fixed (SwrContext** ppSwrContext = &Pointer)
        {
            swr_free(ppSwrContext);
        }
    }
}
