using Suruga.Audio.Primitives;

namespace Suruga.Audio.Commands;

internal abstract record AudioCommand
{
    internal Task<CommandResult> Task => _tcs.Task;
    
    private readonly TaskCompletionSource<CommandResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    internal void SetResult(CommandResult result)
        => _tcs.TrySetResult(result);
    
    internal void SetCanceled(CancellationToken token)
        => _tcs.TrySetCanceled(token);
    
    internal void SetException(Exception exception)
        => _tcs.TrySetException(exception);
}
