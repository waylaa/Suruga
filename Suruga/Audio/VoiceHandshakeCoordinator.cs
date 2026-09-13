using Suruga.Common;

namespace Suruga.Audio;

/// <summary>
/// Tracks the two gateway events Discord sends independently
/// (a VOICE_SERVER_UPDATE and a VOICE_STATE_UPDATE) that are both required
/// before a voice connection can be established, and lets callers wait for
/// both to arrive.
/// </summary>
internal sealed class VoiceHandshakeCoordinator
{
    internal string? Endpoint { get; private set; }

    internal string Token { get; private set; } = string.Empty;

    internal ulong UserId { get; private set; }

    internal ulong? ChannelId { get; private set; }

    internal string SessionId { get; private set; } = string.Empty;
    
    internal ulong? GuildId { get; private set; }

    private readonly Lock _lock = new();

    private TaskCompletionSource _voiceServerTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _voiceStateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal bool OnVoiceServerUpdate(string? endpoint, string token)
    {
        TaskCompletionSource tcs;

        using (_lock.EnterScope())
        {
            Endpoint = endpoint;
            Token = token;
            
            tcs = _voiceServerTcs;
        }

        if (endpoint is null)
        {
            tcs.TrySetCanceled();
            return false;
        }

        tcs.TrySetResult();
        return true;
    }

    internal bool OnVoiceStateUpdate(ulong userId, ulong? channelId, string sessionId)
    {
        TaskCompletionSource tcs;

        using (_lock.EnterScope())
        {
            UserId = userId;
            ChannelId = channelId;
            SessionId = sessionId;
            
            tcs = _voiceStateTcs;
        }

        if (!channelId.HasValue)
        {
            tcs.TrySetCanceled();
            return false;
        }

        tcs.TrySetResult();
        return true;
    }

    internal async Task<bool> WaitForValidVoiceAsync(TimeSpan timeout)
    {
        Task? voiceServerTask;
        Task? voceStateTask;

        using (_lock.EnterScope())
        {
            voiceServerTask = _voiceServerTcs.Task;
            voceStateTask = _voiceStateTcs.Task;
        }

        using CancellationTokenSource cts = new(timeout);

        try
        {
            await Task.WhenAll(voiceServerTask, voceStateTask).WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return false; // Timed out.
        }
        catch (Exception ex)
        {
            Logger.Error<VoiceHandshakeCoordinator>(ex, $"An error occured while waiting for valid voice in guild {GuildId}");
            return false;
        }
    }

    internal void Reset()
    {
        using (_lock.EnterScope())
        {
            Endpoint = null;
            Token = string.Empty;
            UserId = 0;
            ChannelId = null;
            SessionId = string.Empty;
            GuildId = 0;
            
            _voiceServerTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _voiceStateTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
