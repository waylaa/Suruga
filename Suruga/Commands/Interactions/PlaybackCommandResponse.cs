using Suruga.Audio.Primitives;

namespace Suruga.Commands.Interactions;

internal static class PlaybackCommandResponses
{
    internal static string? MessageFor(CommandStatus status) => status switch
    {
        CommandStatus.Success => null, // Caller supplies the success-specific text.
        CommandStatus.AlreadyPaused => "There is nothing to pause.",
        CommandStatus.AlreadyPlaying => "There is nothing to resume.",
        CommandStatus.NothingToSkip => "There is nothing to skip.",
        CommandStatus.AlreadyStopped => "There is nothing to stop.",
        _ => null
    };
}
