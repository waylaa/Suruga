using NetCord.Gateway.Voice;

namespace Suruga.Audio;

internal sealed class AudioSink : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OpusEncodeStream? _encodeStream;

    private bool _isDisposed;
    
    internal async Task WriteAsync(ReadOnlyMemory<byte> pcm, CancellationToken token = default)
    {
        await _readyTcs.Task.WaitAsync(token);
        await _lock.WaitAsync(token);

        try
        {
            if (_encodeStream is null)
            {
                return;
            }

            await _encodeStream.WriteAsync(pcm, token);
        }
        finally
        {
            _lock.Release();
        }
    }

    internal async Task FlushAsync(CancellationToken token = default)
    {
        await _readyTcs.Task.WaitAsync(token);
        await _lock.WaitAsync(token);

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
            _lock.Release();
        }
    }
    
    internal async Task AttachAsync(Stream voiceStream, CancellationToken token = default)
    {
        await _lock.WaitAsync(token);

        try
        {
            if (_encodeStream is not null)
            {
                return;
            }

            _encodeStream = new OpusEncodeStream(voiceStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);
            _readyTcs.TrySetResult();
        }
        finally
        {
            _lock.Release();
        }
    }

    internal async ValueTask DetachAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (_encodeStream is not null)
            {
                await _encodeStream.DisposeAsync();
                _encodeStream = null;
            }

            _readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        await DetachAsync();
    }
}
