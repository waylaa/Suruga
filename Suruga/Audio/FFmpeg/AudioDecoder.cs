using System.Threading.Channels;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Suruga.Audio.FFmpeg.Abstractions;
using Suruga.Transport.Abstractions;

namespace Suruga.Audio.FFmpeg;

internal sealed class AudioDecoder : IDisposable
{
    private readonly ILogger<AudioDecoder> _logger;
    private readonly ByteStreamContext _byteStreamContext;
    private readonly FormatContext _formatContext;
    private readonly CodecContext _codecContext;
    private readonly FilterGraph _filterGraph;
    
    private const int FrameBufferCapacity = 16;

    internal AudioDecoder
    (
        ILogger<AudioDecoder> logger,
        IAudioByteStream byteStream,
        AVSampleFormat outputFormat,
        int outputSampleRate,
        int outputChannels
    )
    {
        _logger = logger;
        _byteStreamContext = new ByteStreamContext(byteStream);
        _formatContext = new FormatContext(_byteStreamContext);
        _codecContext = _formatContext.CreateCodecContext();
        _filterGraph = _codecContext.CreateFilterGraph(outputFormat, outputSampleRate, outputChannels);
    }

    internal IAsyncEnumerable<DecodedFrame<float>> DecodeAsync(CancellationToken token = default)
    {
        Channel<DecodedFrame<float>> frameBuffer = Channel.CreateBounded<DecodedFrame<float>>(new BoundedChannelOptions(FrameBufferCapacity)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _ = Task.Run(() => DecodeLoopAsync(frameBuffer.Writer, token), token);
        return frameBuffer.Reader.ReadAllAsync(token);
    }

    private async Task DecodeLoopAsync(ChannelWriter<DecodedFrame<float>> writer, CancellationToken token = default)
    {
        using Packet packet = _codecContext.CreatePacket();
        using Frame frame = _codecContext.CreateFrame();
        
        try
        {
            while (!token.IsCancellationRequested)
            {
                FFmpegStatus readStatus = _formatContext.ReadFrame(packet);

                if (readStatus is FFmpegStatus.EndOfStream or FFmpegStatus.ByteStreamError)
                {
                    break;
                }

                if (readStatus is FFmpegStatus.Discard)
                {
                    packet.Unreference();
                    continue;
                }

                while (!token.IsCancellationRequested)
                {
                    FFmpegStatus sendStatus = _codecContext.SendPacket(packet);

                    if (sendStatus is FFmpegStatus.Success)
                    {
                        packet.Unreference();
                        break;
                    }

                    if (sendStatus is FFmpegStatus.NeedMoreInput)
                    {
                        // Decoder buffer is full, drain frames before retrying.
                        await DrainDecoderAsync(frame, writer, token);
                        continue;
                    }
                    
                    throw new InvalidOperationException($"Failed to send packet to decoder: {sendStatus}");
                }

                await DrainDecoderAsync(frame, writer, token);
            }

            await FlushAsync(frame, writer, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Decode loop is broken! {Message}", ex.Message);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    internal void SetSpeed(float value)
        => _filterGraph.SetSpeed(value);

    public void Dispose()
    {
        _filterGraph.Dispose();
        _codecContext.Dispose();
        _formatContext.Dispose();
        _byteStreamContext.Dispose();
    }
    
    private async Task DrainDecoderAsync(Frame frame, ChannelWriter<DecodedFrame<float>> writer, CancellationToken token, bool flush = false)
    {
        while (!token.IsCancellationRequested)
        {
            FFmpegStatus receiveStatus = _codecContext.ReceiveFrame(frame);

            if (receiveStatus is FFmpegStatus.NeedMoreInput or FFmpegStatus.EndOfStream && !flush)
            {
                break;
            }
            
            if (receiveStatus is FFmpegStatus.Success)
            {
                _filterGraph.Write(frame);
                frame.Unreference();

                await DrainFilterGraphAsync(writer, token);
            }
            else if (receiveStatus is FFmpegStatus.EndOfStream && flush)
            {
                break;
            }
        }
    }

    private async Task DrainFilterGraphAsync(ChannelWriter<DecodedFrame<float>> writer, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            FFmpegStatus readStatus = _filterGraph.Read(out DecodedFrame<float>? decodedFrame);

            if (readStatus is FFmpegStatus.EndOfStream or FFmpegStatus.ByteStreamError or FFmpegStatus.NeedMoreInput)
            {
                break;
            }

            if (decodedFrame is not null)
            {
                await writer.WriteAsync(decodedFrame, token);
            }
        }
    }

    private async Task FlushAsync(Frame frame, ChannelWriter<DecodedFrame<float>> writer, CancellationToken token)
    {
        _codecContext.SendPacket(null); // Send flush packet to decoder.
        await DrainDecoderAsync(frame, writer, token, flush: true); // Drain remaining frames from decoder.
        _filterGraph.Flush(); // Flush filter graph to get any buffered frames.
        await DrainFilterGraphAsync(writer, token); // Drain remaining frames from filter graph.
    }
}
