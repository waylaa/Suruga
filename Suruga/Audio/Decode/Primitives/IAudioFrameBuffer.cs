namespace Suruga.Audio.Decode.Primitives;

/// <summary>
/// Represents a buffer containing decoded audio samples.
/// </summary>
internal interface IAudioFrameBuffer : IDisposable
{
    /// <summary>
    /// Gets the raw audio data.
    /// </summary>
    ReadOnlyMemory<byte> Buffer { get; }

    /// <summary>
    /// Gets the audio samples.
    /// </summary>
    Span<float> Samples { get; }

    /// <summary>
    /// Gets the number of samples contained in the buffer.
    /// </summary>
    int SampleCount { get; }
}
