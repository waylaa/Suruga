using NetCord.Gateway.Voice;
using Suruga.Common;

namespace Suruga.Audio;

internal sealed class AudioSink : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ManualResetEventSlim _attachedSignal = new(false);
    
    private OpusEncodeStream? _encodeStream;
    private Stream? _voiceStream;
    private bool _isDisposed;
    
    internal async Task WriteAsync(ReadOnlyMemory<byte> pcm, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _attachedSignal.Wait(token);
        await _gate.WaitAsync(token);

        try
        {
            if (_encodeStream is null)
            {
                Logger.Trace<AudioSink>($"WriteAsync called with no encode stream attached. Dropping {pcm.Length} byte(s).");
                return;
            }

            await _encodeStream.WriteAsync(pcm, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task FlushAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        _attachedSignal.Wait(token);
        await _gate.WaitAsync(token);

        try
        {
            if (_encodeStream is null)
            {
                return;
            }

            await _encodeStream.FlushAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }
    
    internal async Task AttachAsync(Stream voiceStream, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(token);

        try
        {
            if (_encodeStream is not null)
            {
                Logger.Debug<AudioSink>("AttachAsync called while already attached. Ignoring.");
                return;
            }
            
            _voiceStream = voiceStream;
            _encodeStream = new OpusEncodeStream(_voiceStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);
            _attachedSignal.Set();
            
            Logger.Debug<AudioSink>("Voice stream attached.");
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async ValueTask DetachAsync()
    {
        await _gate.WaitAsync();

        try
        {
            _attachedSignal.Reset();
            
            if (_encodeStream is not null)
            {
                await _encodeStream.DisposeAsync();
                
                _encodeStream = null;
                _voiceStream = null;
                
                Logger.Debug<AudioSink>("Voice stream has been detached.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;

        try
        {
            await DetachAsync();
        }
        finally
        {
            _gate.Dispose();
        }
    }
}
