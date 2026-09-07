using System.Reflection;
using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Loaders;

public static class FFmpegLibraryLoader
{
    private static string? _customPath;
    private static int _isInitialized;
    
    private static readonly Dictionary<string, int> Versions = new(StringComparer.OrdinalIgnoreCase)
    {
        [FFmpegLibraryNames.AvUtil] = 61,
        [FFmpegLibraryNames.AvCodec] = 63,
        [FFmpegLibraryNames.AvFormat] = 63,
        [FFmpegLibraryNames.AvDevice] = 63,
        [FFmpegLibraryNames.AvFilter] = 12,
        [FFmpegLibraryNames.SwScale] = 10,
        [FFmpegLibraryNames.SwResample] = 7,
    };

    public static void Initialize(string? path = null)
    {
        if (Interlocked.Exchange(ref _isInitialized, 1) != 0)
        {
            return;
        }
        
        _customPath = path;
        NativeLibrary.SetDllImportResolver(typeof(FFmpegLibraryLoader).Assembly, Resolve);
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!Versions.TryGetValue(name, out int version))
        {
            return nint.Zero;
        }
        
        if (_customPath is null)
        {
            throw new InvalidOperationException(
                "FFmpeg path is not configured. Set the 'BOT_FFMPEGPATH' setting to the " +
                "directory containing the FFmpeg shared libraries.");
        }

        foreach (string fileName in CandidateNames(name, version))
        {
            string userPath = Path.Combine(_customPath, fileName);
 
            if (NativeLibrary.TryLoad(userPath, out nint handle))
            {
                return handle;
            }
        }
        
        throw new DllNotFoundException(
            $"Could not load native library '{name}' from '{_customPath}'. Make sure FFmpeg is " +
            "installed there and that 'BOT_FFMPEGPATH' points to the correct directory.");
    }

    private static IEnumerable<string> CandidateNames(string name, int version)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return $"{name}-{version}.dll";
            yield return $"{name}.dll";
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return $"lib{name}.so.{version}";
            yield return $"lib{name}.so";
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }
}
