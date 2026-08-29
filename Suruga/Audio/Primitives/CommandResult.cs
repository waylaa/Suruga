namespace Suruga.Audio.Primitives;

internal readonly record struct CommandResult(CommandStatus Status, object? Data = null)
{
    internal T? GetData<T>() where T : class
        => Data is T value ? value : null;
}
