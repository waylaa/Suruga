namespace Suruga.Audio.Primitives;

internal enum CommandStatus
{
    Success,
    Undefined,
    
    // Connection commands.
    InvalidVoice,
    AlreadyConnected,
    Disconnected,
    
    // Player commands.
    AlreadyPlaying,
    AlreadyStopped,
    AlreadyPaused,
    NothingToPause,
    NothingToResume,
    NothingToSkip,
    NothingToRewind,
    NothingToClear,
    NotEnoughTracksToShuffle,
    UnableToSeek,
    NoTracks
}
