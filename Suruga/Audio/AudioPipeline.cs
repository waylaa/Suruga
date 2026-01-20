using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Suruga.Audio.FFmpeg;
using Suruga.Audio.Primitives;
using Suruga.Audio.Processors;
using Suruga.Transport.Abstractions;

namespace Suruga.Audio;

internal sealed class AudioPipeline
{
    internal float CurrentSpeed
    {
        get;
        set
        {
            _decoder?.SetSpeed(value);
            field = value;
        }
    }

    internal float CurrentVolume
    {
        get;
        set
        {
            _volume.SetVolume(value);
            field = value;
        }
    }

    internal float CurrentPitch
    {
        get;
        set
        {
            // Reset speed to 1 in order to "disable" ffmpeg's
            // atempo filter and use the post-process pitch
            // processor which speeds up audio and pitch.
            if (CurrentSpeed != 1)
            {
                CurrentSpeed = 1;
            }

            _pitch.SetPitch(value);
            field = value;
        }
    }

    private readonly VolumeProcessor _volume;
    private readonly PitchProcessor2 _pitch;
    
    private AudioDecoder? _decoder;

    internal AudioPipeline()
    {
        _volume = new VolumeProcessor();
        _pitch = new PitchProcessor2();
    }

    internal void CreateDecoder
    (
        ILogger<AudioDecoder> decoderLogger,
        IAudioByteStream byteStream,
        SampleFormat format,
        int sampleRate,
        int channels
    )
    {
        _decoder ??= new AudioDecoder(decoderLogger, byteStream, ToFFmpegSampleFormat(format), sampleRate, channels);
    }

    internal async IAsyncEnumerable<DecodedFrame<float>> DecodeAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        if (_decoder is null)
        {
            throw new InvalidOperationException("Decoder is not initialized. Call 'CreateDecoder()' first.");
        }
        
        await foreach (DecodedFrame<float> frame in _decoder.DecodeAsync(token))
        {
            if (!TryPostProcess(frame, out DecodedFrame<float>? processedFrame))
            {
                processedFrame?.Dispose();
                continue;
            }

            yield return processedFrame;
        }
    }

    private bool TryPostProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame)
        => _volume.TryProcess(inputFrame, out outputFrame) && _pitch.TryProcess(inputFrame, out outputFrame);

    public void CloseDecoder()
        => _decoder?.Dispose();

    private static AVSampleFormat ToFFmpegSampleFormat(SampleFormat format) => format switch
    {
        SampleFormat.Float => AVSampleFormat.AV_SAMPLE_FMT_FLT,
        SampleFormat.Int16 => AVSampleFormat.AV_SAMPLE_FMT_S16,
        _ => throw new ArgumentException("Invalid sample format.", nameof(format))
    };
}