using System.Diagnostics.CodeAnalysis;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Suruga.Audio.Decode.Primitives;
using Suruga.Transport;

namespace Suruga.Audio.Decode;

/// <summary>
/// Decodes audio data.
/// </summary>
internal sealed partial class AudioDecoder : IDisposable
{
    private readonly ByteStreamContext _byteStreamContext;
    private readonly FormatContext _formatContext;
    private readonly CodecContext _codecContext;
    private readonly ResamplerContext _resamplerContext;
    private readonly Packet _packet;
    private readonly Frame _frame;
    private readonly ILogger<AudioDecoder> _logger;
    private readonly Lock _lock = new();
    
    private bool _isEndOfStream;
    private bool _isDisposed;
    
    /// <summary>
    /// Initializes the audio decoder.
    /// </summary>
    /// <param name="byteStream">The source audio stream.</param>
    /// <param name="logger">The logger used for diagnostics.</param>
    internal AudioDecoder(ReadOnlyAudioByteStream byteStream, ILogger<AudioDecoder> logger)
    {
        _byteStreamContext = new ByteStreamContext(byteStream);
        _formatContext = new FormatContext(_byteStreamContext);
        
        unsafe
        {
            _formatContext.GetStream(out AVCodec* pCodec, out AVStream* pStream);
            _codecContext = new CodecContext(pCodec, pStream);
        }

        _resamplerContext = new ResamplerContext(_codecContext.ChannelLayout, _codecContext.SampleFormat, _codecContext.SampleRate);
        _packet = new Packet();
        _frame = new Frame();
        _logger = logger;
    }
    
    /// <summary>
    /// Attempts to decode the next audio frame.
    /// </summary>
    /// <param name="frame">
    /// When this method returns <see langword="true"/>, contains the decoded
    /// audio frame; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a frame was decoded; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool TryDecodeFrame([NotNullWhen(true)] out IAudioFrameBuffer? frame)
    {
        using (_lock.EnterScope())
        {
            frame = null;

            while (true)
            {
                // No full frame yet, we need to decode more data into the queue.
                // If the stream already signaled EOS, we cannot feed any more packets,
                // so there is nothing left to return.
                if (_isEndOfStream)
                {
                    return false;
                }

                if (!TryReceiveFrame())
                {
                    continue;
                }
            
                frame = _resamplerContext.Resample(_frame);

                // UnmanagedAudioFrameBuffer holds the underlying Frame object
                // and unreferences it when disposed after writing samples to voice
                // or when post-processing.
                if (frame is ManagedAudioFrameBuffer)
                {
                    _frame.Unreference();
                }
                
                return true;
            }   
        }
    }

    /// <summary>
    /// Seeks the decoder to the specified position.
    /// </summary>
    /// <param name="timestamp">The target playback position.</param>
    internal void Seek(TimeSpan timestamp)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using (_lock.EnterScope())
        {
            _codecContext.FlushBuffers();
            _formatContext.SeekFrame(timestamp);
            
            _isEndOfStream = false;
        }
        
        LogSeek(timestamp);
    }

    /// <summary>
    /// Flushes any buffered decoder state.
    /// </summary>
    internal void Flush()
    {
        using (_lock.EnterScope())
        {
            _codecContext.FlushBuffers();
            _codecContext.SendPacket(null);
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        
        _frame.Dispose();
        _packet.Dispose();
        _resamplerContext.Dispose();
        _codecContext.Dispose();
        _formatContext.Dispose();
        _byteStreamContext.Dispose();
    }

    /// <summary>
    /// Attempts to receive the next decoded frame from the codec.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if decoding can continue; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    private bool TryReceiveFrame()
    {
        while (true)
        {
            // Try to receive a frame that the decoded has already buffered.
            FFmpegResponse receiveResponse = _codecContext.ReceiveFrame(_frame);

            switch (receiveResponse)
            {
                case FFmpegResponse.Success:
                    return true;
                
                case FFmpegResponse.NeedMoreInput:
                    // The decoder needs another packet before it can produce a frame.
                    if (!TrySendPacketToDecoder())
                        return false; // Unrecoverable byte stream error.
                    continue;
                
                case FFmpegResponse.EndOfStream:
                    _isEndOfStream = true;
                    return true; // Let the caller drain whatever is left in the queue.

                case FFmpegResponse.ByteStreamError or FFmpegResponse.InvalidData or FFmpegResponse.Discard:
                    _logger.LogWarning("FFmpeg status: {Status}", receiveResponse);
                    return false;
                
                default:
                    _logger.LogWarning("Unknown FFmpeg status: {Status}", receiveResponse);
                    return false;
            }
        }
    }

    /// <summary>
    /// Reads the next packet and submits it to the decoder.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if decoding can continue; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TrySendPacketToDecoder()
    {
        while (true)
        {
            try
            {
                FFmpegResponse readResponse = _formatContext.ReadFrame(_packet);

                switch (readResponse)
                {
                    case FFmpegResponse.Success:
                        _codecContext.SendPacket(_packet);
                        _packet.Unreference();
                        return true;

                    case FFmpegResponse.Discard or FFmpegResponse.InvalidData:
                        _packet.Unreference();
                        continue; // Skip non-audio or corrupt packets.

                    case FFmpegResponse.EndOfStream:
                        // Signal the decoder to flush its internal buffers.
                        _codecContext.SendPacket(null);
                        return true;

                    case FFmpegResponse.ByteStreamError:
                        _logger.LogError("Byte stream error while reading packet");
                        return false;

                    default:
                        _logger.LogWarning("Unexpected read status: {Status}", readResponse);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read next packet");
                return false;
            }
        }
    }
    
    [LoggerMessage(LogLevel.Trace, Message = "Seeking to {Timestamp}")]
    private partial void LogSeek(TimeSpan timestamp);
}
