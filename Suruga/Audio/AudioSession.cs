using NetCord.Rest;
using Suruga.Audio.Events;

namespace Suruga.Audio;

internal sealed class AudioSession : IAsyncDisposable
{
    internal AudioPlayer Player { get; }

    internal PlayerMessageUpdater PlayerMessage { get; } = new();

    internal RestClient Rest { get; }

    internal ulong TextChannelId { get; }
    
    private bool _isDisposed;

    internal AudioSession(AudioPlayer player, RestClient rest, ulong textChannelId)
    {
        Player = player;
        Rest = rest;
        TextChannelId = textChannelId;

        Player.PlayerStateChanged += OnPlayerStateChangedAsync;
    }

    private Task OnPlayerStateChangedAsync(PlayerStateChangedEventArgs args)
        => PlayerMessage.UpdateAsync(args);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        Player.PlayerStateChanged -= OnPlayerStateChangedAsync;
        await Player.DisposeAsync();
    }
}
