namespace Suruga.Audio.Primitives;

internal readonly record struct CommandResult(CommandStatus Status, object? Data = null);
