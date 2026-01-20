using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using Suruga.Audio.Extensions;
using Suruga.Audio.FFmpeg;
using Suruga.Audio.Primitives;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Transport;
using Suruga.Transport.Abstractions;
using Suruga.Transport.Factories;

namespace Suruga.Audio;

internal sealed class AudioPlayer : IAsyncDisposable
{
    internal event Func<AudioTrack, Task>? TrackStarted;
    internal event Func<AudioTrack, Task>? TrackEnded;
    internal event Func<AudioTrack, Exception, Task>? TrackErrored;

    internal IReadOnlyList<AudioTrack> Queue => _queue.PeekAll();

    private readonly ILogger<AudioPlayer> _logger;
    private readonly ILogger<AudioDecoder> _decoderLogger;
    private readonly VoiceClient _voice;
    private readonly CompositeAudioSourceResolver _sourceResolver;
    private readonly CompositeAudioByteStreamFactory _audioByteStreamFactory;
    private readonly AudioPipeline _pipeline = new();
    private readonly CancellationTokenSource _playerCts;
    private readonly AudioTrackQueue _queue = new();
    
    private Task? _playbackTask;
    private CancellationTokenSource? _trackCts;
    private bool _isDisposed;
    
    internal AudioPlayer
    (
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime,
        VoiceClient voice,
        CompositeAudioSourceResolver resolver,
        CompositeAudioByteStreamFactory audioByteStreamFactory
    )
    {
        _logger = loggerFactory.CreateLogger<AudioPlayer>();
        _decoderLogger = loggerFactory.CreateLogger<AudioDecoder>();
        _voice = voice;
        _sourceResolver = resolver;
        _audioByteStreamFactory = audioByteStreamFactory;

        _playerCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        _playbackTask = Task.Run(PlaybackLoopAsync);
    }

    internal async Task<Result<IReadOnlyList<AudioTrack>>> EnqueueAsync(string input)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Result<AudioSource> sourceResult = await _sourceResolver.ResolveAsync(input, _playerCts.Token);
        
        if (!sourceResult.TryGetValue(out AudioSource? source))
        {
            return Result<IReadOnlyList<AudioTrack>>.Failure(sourceResult.Error);
        }

        if (source.Tracks.Count == 0)
        {
            return Result<IReadOnlyList<AudioTrack>>.Failure("Audio source has no tracks.");
        }

        foreach (AudioTrack track in source.Tracks)
        {
            _queue.Enqueue(track);
        }
        
        return Result<IReadOnlyList<AudioTrack>>.Success(source.Tracks);
    }

    internal async Task LeaveAsync()
    {
        await _playerCts.CancelAsync();

        // Wait for graceful completion.
        if (_playbackTask is not null)
        {
            await _playbackTask;
        }
        
        if (_voice.Status is not WebSocketStatus.Disconnected)
        {
            await _voice.CloseAsync();
        }
    }

    private async Task PlaybackLoopAsync()
    {
        while (!_playerCts.Token.IsCancellationRequested)
        {
            AudioTrack? track = null;

            try
            {
                track = _queue.Dequeue();

                if (track is null)
                {
                    continue;
                }

                if (TrackStarted is not null)
                {
                    await TrackStarted.InvokeAsync(track);
                }

                await PlayTrackAsync(track, _playerCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Playback loop exception for track {TrackTitle}", track?.Title ?? "<none>");
                }
            }
        }
    }

    private async Task PlayTrackAsync(AudioTrack track, CancellationToken playerToken)
    {
        _trackCts?.Dispose();
        _trackCts = CancellationTokenSource.CreateLinkedTokenSource(playerToken);
        CancellationToken trackToken = _trackCts.Token;

        try
        {
            if (_voice.Status is not WebSocketStatus.Ready)
            {
                await _voice.StartAsync(trackToken);
                await _voice.EnterSpeakingStateAsync
                (
                    new SpeakingProperties(SpeakingFlags.Microphone),
                    cancellationToken: trackToken
                );
            }

            Result<IAudioByteStream> byteStreamResult = _audioByteStreamFactory.Create(track);

            if (!byteStreamResult.TryGetValue(out IAudioByteStream? byteStream))
            {
                throw byteStreamResult.Error;
            }
            
            _pipeline.CreateDecoder(_decoderLogger, byteStream, SampleFormat.Float, 48000, 2);

            await using Stream discordStream = _voice.CreateOutputStream();
            await using OpusEncodeStream opusStream = new(discordStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);
            
            await foreach (DecodedFrame<float> frame in _pipeline.DecodeAsync(trackToken))
            {
                using (frame)
                {
                    DecodedFrame<float>.ReadOnlyDecodedFrameView view = frame.GetReadOnlyView();
                    ReadOnlySpan<byte> samples = MemoryMarshal.AsBytes(view.Buffer);
                    opusStream.Write(samples);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (TrackErrored is not null)
            {
                await TrackErrored.InvokeAsync(track, ex);
            }
        }
        finally
        {
            if (TrackEnded is not null)
            {
                await TrackEnded.Invoke(track);
            }
            
            _pipeline.CloseDecoder();
            _trackCts?.Dispose();
            _trackCts = null;
        }
    }
    
    internal void SetSpeed(float value)
        => _pipeline.CurrentSpeed = value;
    
    internal void SetVolume(int value)
        => _pipeline.CurrentVolume =  value / 100f;
    
    internal void SetPitch(float value)
        => _pipeline.CurrentPitch = value;
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        await _playerCts.CancelAsync();

        if (_playbackTask is not null)
        {
            try
            {
                await _playbackTask;
            }
            catch
            {
            }
        }

        _trackCts?.Dispose();
        _pipeline.CloseDecoder();

        if (_voice.Status is not WebSocketStatus.Disconnected)
        {
            await _voice.CloseAsync();
        }

        _playerCts.Dispose();
    }
}
