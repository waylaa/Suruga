using System.Diagnostics.CodeAnalysis;

namespace Suruga.Audio.Processors.Abstractions;

internal interface IAudioProcessor
{
    /// <summary>
    /// Processes a decoded audio frame.
    /// </summary>
    /// <remarks>This method is zero-copy.</remarks>
    /// <returns>
    /// [<see cref="bool"/>] <c>True</c> if a frame has been processed successfully,
    /// otherwise <c>false</c>.
    /// </returns>
    bool TryProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame);
}
