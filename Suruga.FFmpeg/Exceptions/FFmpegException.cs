using System.Runtime.InteropServices;
using System.Text;
using Suruga.FFmpeg.Interop;

namespace Suruga.FFmpeg.Exceptions;

internal sealed unsafe class FFmpegException : Exception
{
    internal int ErrorCode { get; }

    internal FFmpegException(int errorCode) : base(GetDescription(errorCode))
        => ErrorCode = errorCode;

    internal FFmpegException(int errorCode, string context) : base($"{context}: {GetDescription(errorCode)}")
        => ErrorCode = errorCode;

    private static string GetDescription(int errorCode)
    {
        Span<byte> buffer = stackalloc byte[Constants.AV_ERROR_MAX_STRING_SIZE];
        int result = NativeMethods.av_strerror(errorCode, ref MemoryMarshal.GetReference(buffer), Constants.AV_ERROR_MAX_STRING_SIZE);

        if (result < 0)
        {
            return $"Unknown FFmpeg error ({errorCode}).";
        }
        
        return Encoding.UTF8.GetString(buffer);
    }
}
