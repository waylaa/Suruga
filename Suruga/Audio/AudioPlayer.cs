using Microsoft.Extensions.Logging;
using Suruga.Audio.Commands.Playback;
using Suruga.Audio.Primitives;
using Suruga.Persistence;
using Suruga.PostProcessing;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioPlayer : IAsyncDisposable
{
    internal event Func<AudioPlayerState, Track?, Exception?, Task>? PlayerStateChanged
    {
        add => _stateNotifier.StateChanged += value;
        remove => _stateNotifier.StateChanged -= value;
    }

    internal TrackQueue Queue { get; }

    internal AudioPlayerState State => _engine.State;

    private readonly TrackPlaybackEngine _engine;
    private readonly PlaybackCommandDispatcher _dispatcher;
    private readonly PlayerStateNotifier _stateNotifier = new();

    internal AudioPlayer
    (
        TrackStreamResolverRouter resolverRouter,
        ReadOnlyAudioByteStreamFactory byteStreamFactory,
        TrackQueueStateRepository repository,
        AudioSink sink,
        ILoggerFactory loggerFactory,
        ulong guildId
    )
    {
        AudioPostProcessor postProcessor = new(loggerFactory);
        Queue = new TrackQueue(repository, guildId);

        _engine = new TrackPlaybackEngine(Queue, resolverRouter, byteStreamFactory, sink, postProcessor, _stateNotifier, loggerFactory);
        _dispatcher = new PlaybackCommandDispatcher(_engine, Queue, postProcessor);
    }

    internal Task<CommandResult> PostAsync(PlaybackCommand command, CancellationToken token = default)
        => _dispatcher.PostAsync(command, token);

    public async ValueTask DisposeAsync()
    {
        await _dispatcher.DisposeAsync();
        await _engine.DisposeAsync();
    }
}
