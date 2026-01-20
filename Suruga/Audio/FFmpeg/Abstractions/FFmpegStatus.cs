namespace Suruga.Audio.FFmpeg.Abstractions;

internal enum FFmpegStatus
{
    Success,
    EndOfStream,
    NeedMoreInput,
    ByteStreamError,
    Discard
}

