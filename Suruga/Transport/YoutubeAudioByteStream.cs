using System.Buffers;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using Suruga.Primitives;
using Suruga.Resolvers.Youtube.Clients.Abstractions;
using Suruga.Transport.Abstractions;

namespace Suruga.Transport;

internal sealed class YoutubeAudioByteStream : IAudioByteStream
{
    public long? Length => null; // Unknown/streaming.

    private readonly Func<Task<Result<AudioSource>>> _resolveDelegate;
    private readonly YoutubeClientBase _boundClient;
    private readonly Pipe _pipe;
    private readonly int _readBufferSize;

    private CancellationTokenSource _cts = new();
    private Task _producerTask;

    private Stream? _httpStream;
    private Exception? _fault;

    private DateTimeOffset _absoluteExpiry;
    private string _currentUrl;
    private long _position;

    public YoutubeAudioByteStream(AudioTrack track, int readBufferSize = 64 * 1024, int maxPipeBufferSize = 512 * 1024)
    {
        if (track.Callback is null || track.Callback is not Func<Task<Result<AudioSource>>> resolveCallback || track.CallbackInfo is null)
        {
            throw new ArgumentException("Track must have a valid resolve callback and callback info.", nameof(track));
        }

        _resolveDelegate = resolveCallback;
        _boundClient = (YoutubeClientBase)track.CallbackInfo["Client"];
        _currentUrl = track.StreamUrl;
        _absoluteExpiry = DateTimeOffset.UtcNow + (TimeSpan)track.CallbackInfo["Expiry"];
        _readBufferSize = readBufferSize;

        _pipe = new Pipe(new PipeOptions
        (
            pauseWriterThreshold: maxPipeBufferSize,
            resumeWriterThreshold: maxPipeBufferSize / 2,
            useSynchronizationContext: false
        ));

        _producerTask = Task.Run(() => ProducerLoopAsync(_cts.Token));
    }

    public int Read(Span<byte> destination)
    {
        if (_fault is not null)
        {
            throw _fault;
        }

        PipeReader reader = _pipe.Reader;

        ReadResult result = reader.ReadAsync().GetAwaiter().GetResult();
        ReadOnlySequence<byte> bufferSegment = result.Buffer;

        if (bufferSegment.Length == 0 && result.IsCompleted)
        {
            reader.AdvanceTo(bufferSegment.End);
            return 0; // EOF
        }

        int read = (int)Math.Min(bufferSegment.Length, destination.Length);
        bufferSegment.Slice(0, read).CopyTo(destination);

        reader.AdvanceTo(bufferSegment.GetPosition(read));
        _position += read;

        return read;
    }

    public long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin && offset == 0)
        {
            RestartProducer();
            return 0;
        }

        throw new NotSupportedException
        (
            "Byte-level seeking is not supported for streaming audio. " +
            "Use timestamp-based seeking at the decoder level."
        );
    }

    private void RestartProducer()
    {
        StopProducer();

        _position = 0;
        _httpStream?.Dispose();
        _httpStream = null;

        _pipe.Reader.Complete();
        _pipe.Writer.Complete();
        _pipe.Reset();

        _cts = new CancellationTokenSource();
        _producerTask = Task.Run(() => ProducerLoopAsync(_cts.Token));
    }

    private async Task ProducerLoopAsync(CancellationToken ct)
    {
        PipeWriter writer = _pipe.Writer;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow >= _absoluteExpiry)
                {
                    await RefreshUrlAsync(ct);
                }

                if (_httpStream is null)
                {
                    Result<Stream> openResult = await OpenHttpStreamAsync(_position, ct);

                    if (openResult.TryGetValue(out _httpStream))
                    {
                        await Task.Delay(500, ct);
                        continue;
                    }
                }
                
                if (_httpStream is null)
                {
                    break;
                }
                
                Memory<byte> buffer = writer.GetMemory(_readBufferSize);
                int readBytes = await _httpStream.ReadAsync(buffer, ct);

                if (readBytes == 0)
                {
                    break;
                }

                writer.Advance(readBytes);
                FlushResult flush = await writer.FlushAsync(ct);

                if (flush.IsCompleted || flush.IsCanceled)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) // Normal shutdown.
        {
        }
        catch (Exception ex)
        {
            _fault = ex;
            await writer.CompleteAsync(ex);

            return;
        }

        await writer.CompleteAsync();
    }

    private async Task<Result<Stream>> OpenHttpStreamAsync(long position, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, _currentUrl);

        if (position > 0)
        {
            request.Headers.Range = new RangeHeaderValue(from: position, to: null);
        }

        Result<HttpResponseMessage> responseResult = await _boundClient.GetResponseAsync(request, ct);

        if (!responseResult.TryGetValue(out HttpResponseMessage? response))
        {
            return Result<Stream>.Failure(responseResult.Error);
        }

        // Do not dispose response, stream depends on it.
        Stream stream = await response.Content.ReadAsStreamAsync(ct);
        return Result<Stream>.Success(stream);
    }

    private async Task RefreshUrlAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        Result<AudioSource> result = await _resolveDelegate();

        if (!result.TryGetValue(out AudioSource? source))
        {
            throw result.Error;
        }

        AudioTrack track = source.Tracks[0];

        if (track.CallbackInfo is null)
        {
            throw new InvalidOperationException("Resolved track must have callback info.");
        }

        _currentUrl = track.StreamUrl;
        _absoluteExpiry = DateTimeOffset.UtcNow + (TimeSpan)track.CallbackInfo["Expiry"];

        // Close existing stream to force reconnect with new URL
        if (_httpStream is not null)
        {
            await _httpStream.DisposeAsync();
        }
        
        _httpStream = null;
    }

    private void StopProducer()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            // Wait with timeout to prevent hanging.
            _producerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Swallow exceptions during shutdown.
        }

        _cts.Dispose();
    }

    public void Dispose()
    {
        StopProducer();

        _httpStream?.Dispose();
        _pipe.Reader.Complete();
        _pipe.Writer.Complete();
    }
}
