namespace Suruga.Audio.Primitives;

internal sealed class PauseTokenSource
{
    internal PauseToken Token => new(this);
    
    internal bool IsPaused
    {
        get => _pausedTcs is not null;
        set
        {
            if (value)
            {
                TaskCompletionSource<bool> newTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.CompareExchange(ref _pausedTcs, newTcs, null);
            }
            else
            {
                TaskCompletionSource<bool>? previousTcs = Interlocked.Exchange(ref _pausedTcs, null);
                previousTcs?.TrySetResult(true);
            }
        }
    }
    
    private TaskCompletionSource<bool>? _pausedTcs;

    private async Task WaitWhilePausedAsync(Func<Task>? beforeWait = null)
    {
        if (beforeWait is not null)
        {
            await beforeWait();
        }
        
        TaskCompletionSource<bool>? pausedTcs = _pausedTcs;

        if (pausedTcs is not null)
        {
            await pausedTcs.Task;
        }
    }

    internal readonly struct PauseToken(PauseTokenSource? source)
    {
        private bool IsPaused => source?.IsPaused ?? false;
        
        internal Task WaitWhilePausedAsync(Func<Task>? beforeWait = null)
            => IsPaused ? source!.WaitWhilePausedAsync(beforeWait) : Task.CompletedTask;
    }
}
