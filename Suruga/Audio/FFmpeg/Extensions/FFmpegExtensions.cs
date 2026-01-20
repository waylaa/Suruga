using System.Text;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg.Extensions;

internal static class FFmpegExtensions
{
    extension(int returnCode)
    {
        internal unsafe void ThrowOnError(Action? onError = null)
        {
            if (returnCode >= 0)
            {
                return;
            }

            onError?.Invoke();

            Span<byte> buffer = stackalloc byte[AV_ERROR_MAX_STRING_SIZE];

            fixed (byte* bufferPointer = buffer)
            {
                av_make_error_string(bufferPointer, AV_ERROR_MAX_STRING_SIZE, returnCode);
            }

            // Slice up to the first null terminator.
            int length = buffer.IndexOf(byte.MinValue);

            if (length == -1)
            {
                length = buffer.Length; // If the null terminator is not found, return the original length.
            }

            throw new Exception(Encoding.UTF8.GetString(buffer[..length]));
        }
    }
}
