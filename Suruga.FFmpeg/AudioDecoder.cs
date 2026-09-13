using System.Diagnostics.CodeAnalysis;
using Suruga.Common;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg;

public sealed class AudioDecoder : IDisposable
{
    private readonly InputOutputContext _ioContext;
    private readonly FormatContext _formatContext;
    private readonly CodecContext _codecContext;
    private readonly ResamplerContext _resamplerContext;
    private readonly Packet _packet;
    private readonly Frame _frame;

    private bool _isPacketPending;
    private bool _isEndOfInput;
    private bool _isDisposed;

    public AudioDecoder(Stream byteStream)
    {
        _ioContext = new InputOutputContext(byteStream);
        _formatContext = new FormatContext(_ioContext);
        _codecContext = new CodecContext(_formatContext.GetStream());
        _resamplerContext = new ResamplerContext(_codecContext);
        _packet = new Packet();
        _frame = new Frame();
    }

    public bool TryDecodeNextFramebuffer([NotNullWhen(true)] out AudioFramebuffer? chunk, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        chunk = null;

        while (!token.IsCancellationRequested)
        {
            FFmpegResult receiveResult = _codecContext.ReceiveFrame(_frame);

            if (receiveResult is FFmpegResult.NeedMoreInput)
            {
                if (!TrySendNextPacket())
                {
                    return false;
                }

                continue;
            }

            if (receiveResult is FFmpegResult.EndOfStream)
            {
                return false;
            }

            if (receiveResult is not FFmpegResult.Success)
            {
                throw new InvalidOperationException($"Unknown decoder error ({receiveResult}).");
            }

            try
            {
                chunk = _resamplerContext.Resample(_frame);
            }
            finally
            {
                _frame.Unreference();
            }

            return true;
        }

        return false;
    }

    public IEnumerable<AudioFramebuffer> Flush()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _codecContext.SendPacket(null);

        while (_codecContext.ReceiveFrame(_frame) is FFmpegResult.Success)
        {
            AudioFramebuffer framebuffer;

            try
            {
                framebuffer = _resamplerContext.Resample(_frame);
            }
            finally
            {
                _frame.Unreference();
            }
            
            yield return framebuffer;
        }
    }

    public bool TrySeek(TimeSpan timestamp)
    {
        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _formatContext.Seek(timestamp);
            _codecContext.Flush();
            _resamplerContext.Reset();

            if (_isPacketPending)
            {
                _packet.Unreference();
                _isPacketPending = false;
            }

            _isEndOfInput = false;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error<AudioDecoder>(ex, "A decoder error occurred during seeking.");
            return false;
        }
    }

    private bool TrySendNextPacket()
    {
        while (true)
        {
            if (_isPacketPending)
            {
                FFmpegResult sendResult = _codecContext.SendPacket(_packet);

                if (sendResult is FFmpegResult.NeedMoreInput)
                {
                    return true;
                }

                _packet.Unreference();
                _isPacketPending = false;
                
                return true;
            }

            if (_isEndOfInput)
            {
                return false;
            }

            FFmpegResult readResult = _formatContext.ReadFrame(_packet);

            if (readResult is FFmpegResult.Discard or FFmpegResult.NeedMoreInput)
            {
                _packet.Unreference();
                continue;
            }

            if (readResult is FFmpegResult.InputOutputError)
            {
                throw new IOException("Failed to read from the underlying audio stream.");
            }

            if (readResult is FFmpegResult.EndOfStream)
            {
                _packet.Unreference();
                _isEndOfInput = true;
                
                continue;
            }

            if (readResult is not FFmpegResult.Success)
            {
                throw new InvalidOperationException($"Unknown demuxer error ({readResult}).");
            }

            _isPacketPending = true;
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
        _ioContext.Dispose();
    }
}
