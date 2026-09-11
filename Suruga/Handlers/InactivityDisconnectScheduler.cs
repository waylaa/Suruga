using System.Collections.Concurrent;

namespace Suruga.Handlers;

internal sealed class InactivityDisconnectScheduler(TimeSpan delay)
{
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _timers = [];

    internal bool TrySchedule(ulong voiceChannelId, Func<CancellationToken, Task> onElapsed)
    {
        CancellationTokenSource cts = new();

        if (!_timers.TryAdd(voiceChannelId, cts))
        {
            cts.Dispose();
            return false;
        }

        _ = RunAsync(voiceChannelId, cts, onElapsed);
        return true;
    }

    internal async Task CancelAsync(ulong voiceChannelId)
    {
        if (_timers.TryRemove(voiceChannelId, out CancellationTokenSource? cts))
        {
            await cts.CancelAsync();
        }
    }

    private async Task RunAsync(ulong voiceChannelId, CancellationTokenSource cts, Func<CancellationToken, Task> onElapsed)
    {
        try
        {
            await Task.Delay(delay, cts.Token);
            await onElapsed(cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Ignore.
        }
        finally
        {
            if (_timers.TryRemove(voiceChannelId, out CancellationTokenSource? current) && ReferenceEquals(current, cts))
            {
                // Already removed above in the common path; guards double-dispose on race.
            }

            cts.Dispose();
        }
    }
}
