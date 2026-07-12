namespace Suruga.Audio.Decode.Primitives;

/// <summary>
/// Represents the response value of an FFmpeg operation.
/// </summary>
internal enum FFmpegResponse
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success,
    
    /// <summary>
    /// The end of the stream has been reached.
    /// </summary>
    EndOfStream,
    
    /// <summary>
    /// More input data is required to proceed.
    /// </summary>
    NeedMoreInput,
    
    /// <summary>
    /// An error occurred while reading from the byte stream.
    /// </summary>
    ByteStreamError,

    /// <summary>
    /// The input data is invalid or could not be decoded.
    /// </summary>
    InvalidData,
    
    /// <summary>
    /// The current packet or frame should be discarded.
    /// </summary>
    Discard
}
