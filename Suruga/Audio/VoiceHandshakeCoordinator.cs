namespace Suruga.Audio;

internal sealed class VoiceHandshakeCoordinator
{
    internal string? Endpoint { get; private set; }
    
    internal string Token { get; private set; } = string.Empty;
    
    internal ulong UserId { get; private set; }
    
    internal ulong? ChannelId { get; private set; }
    
    internal string SessionId { get; private set; } = string.Empty;

    private TaskCompletionSource _voiceServerTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _voiceStateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal bool OnVoiceServerUpdate(string? endpoint, string token)
    {
        Endpoint = endpoint;
        Token = token;

        if (endpoint is null)
        {
            _voiceServerTcs.TrySetCanceled();
            return false;
        }

        _voiceServerTcs.TrySetResult();
        return true;
    }

    internal bool OnVoiceStateUpdate(ulong userId, ulong? channelId, string sessionId)
    {
        UserId = userId;
        ChannelId = channelId;
        SessionId = sessionId;

        if (!channelId.HasValue)
        {
            _voiceStateTcs.TrySetCanceled();
            return false;
        }

        _voiceStateTcs.TrySetResult();
        return true;
    }

    internal async Task<bool> WaitForValidVoiceAsync(TimeSpan timeout)
    {
        Task waitTask = Task.WhenAll(_voiceServerTcs.Task, _voiceStateTcs.Task);
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));

        return completedTask == waitTask &&
               _voiceServerTcs.Task.IsCompletedSuccessfully &&
               _voiceStateTcs.Task.IsCompletedSuccessfully;
    }

    internal void Reset()
    {
        _voiceServerTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _voiceStateTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
