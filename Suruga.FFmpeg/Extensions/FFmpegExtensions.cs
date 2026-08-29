using System.Runtime.CompilerServices;
using Suruga.FFmpeg.Exceptions;

namespace Suruga.FFmpeg.Extensions;

internal static class FFmpegExtensions
{
    extension(int returnCode)
    {
        internal int ThrowIfError(Action? onError = null, [CallerMemberName] string? context = null)
        {
            if (returnCode >= 0)
            {
                return returnCode;
            }
            
            onError?.Invoke();
            throw context is null ? new FFmpegException(returnCode) : new FFmpegException(returnCode, context);
        }

        internal int ThrowIfError(Predicate<int> predicate, Action? onError = null, [CallerMemberName] string? context = null)
        {
            if (!predicate(returnCode))
            {
                return returnCode;
            }
            
            onError?.Invoke();
            throw context is null ? new FFmpegException(returnCode) : new FFmpegException(returnCode, context);
        }
    }
}
