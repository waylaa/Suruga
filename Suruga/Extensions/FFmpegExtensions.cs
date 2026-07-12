using System.Text;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Extensions;

/// <summary>
/// Provides extension methods for FFmpeg-related return codes and error handling.
/// </summary>
internal static class FFmpegExtensions
{
    /// <summary>
    /// Throws an exception containing a human-readable FFmpeg error message
    /// if the specified predicate evaluates to <see langword="true"/>.
    /// </summary>
    /// <param name="returnCode">The FFmpeg function return code.</param>
    /// <param name="predicate">
    /// A function that determines whether the return code represents an error.
    /// </param>
    /// <param name="onError">
    /// An optional callback executed before the exception is thrown.
    /// </param>
    /// <returns>
    /// The original <paramref name="returnCode"/> if no error is detected.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown when <paramref name="predicate"/> returns <see langword="true"/>,
    /// containing the FFmpeg-formatted error message.
    /// </exception>
    internal static unsafe int ThrowOnError(this int returnCode, Predicate<int> predicate, Action? onError = null)
    {
        if (!predicate(returnCode))
        {
            return returnCode;
        }

        onError?.Invoke();
        Span<byte> buffer = stackalloc byte[AV_ERROR_MAX_STRING_SIZE];

        fixed (byte* pBuffer = buffer)
        {
            av_make_error_string(pBuffer, AV_ERROR_MAX_STRING_SIZE, returnCode);
        }

        string message = Encoding.UTF8.GetString(buffer).TrimEnd();
        throw new Exception(message);
    }
}
