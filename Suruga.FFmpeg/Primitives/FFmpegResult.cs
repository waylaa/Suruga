namespace Suruga.FFmpeg.Primitives;

public enum FFmpegResult
{
    Success,
    EndOfStream,
    NeedMoreInput,
    InputOutputError,
    Discard
}
