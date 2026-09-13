namespace Suruga.Audio.Primitives;

internal sealed class PlaybackPauseController
{
    private TaskCompletionSource? _pauseGate;

    internal void Pause()
    {
        if (Volatile.Read(ref _pauseGate) is not null)
        {
            return;
        }
        
        TaskCompletionSource newGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        if (Interlocked.CompareExchange(ref _pauseGate, newGate, null) is null)
        {
            // Another thread set the gate first.
        }
    }

    internal void Resume()
    {
        TaskCompletionSource? gate = Interlocked.Exchange(ref _pauseGate, null);
        gate?.TrySetResult();
    }

    internal async Task WaitWhilePausedAsync(Func<Task>? onSuspend = null, CancellationToken token = default)
    {
        TaskCompletionSource? gate = Volatile.Read(ref _pauseGate);

        if (gate is null)
        {
            return;
        }

        if (onSuspend is not null)
        {
            await onSuspend();
        }

        await gate.Task.WaitAsync(token);
    }
}
