using System.Reflection;
using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Loaders;

public static class FFmpegLibraryLoader
{
    private static string? _customPath;
    private static int _isInitialized;
    
    private static readonly Dictionary<string, int> Versions = new(StringComparer.OrdinalIgnoreCase)
    {
        [FFmpegLibraryNames.AvUtil] = 60,
        [FFmpegLibraryNames.AvCodec] = 62,
        [FFmpegLibraryNames.AvFormat] = 62,
        [FFmpegLibraryNames.AvDevice] = 62,
        [FFmpegLibraryNames.AvFilter] = 11,
        [FFmpegLibraryNames.SwScale] = 9,
        [FFmpegLibraryNames.SwResample] = 6,
    };

    public static void Initialize(string? path = null)
    {
        if (Interlocked.Exchange(ref _isInitialized, 1) == 1)
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

        foreach (string fileName in CandidateNames(name, version))
        {
            // User-specified path to runtimes.
            if (_customPath is not null)
            {
                string userPath = Path.Combine(_customPath, fileName);
                
                if (File.Exists(userPath) && NativeLibrary.TryLoad(userPath, out nint userHandle))
                {
                    return userHandle;
                }
            }
            
            if (NativeLibrary.TryLoad(fileName, assembly, searchPath, out nint handle))
            {
                return handle;
            }

            string rid = RuntimeInformation.RuntimeIdentifier;
            string ridPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);

            // Bundled runtimes
            if (File.Exists(ridPath) && NativeLibrary.TryLoad(ridPath, out handle))
            {
                return handle;
            }
            
            // System-wide
            if (NativeLibrary.TryLoad(fileName, assembly, searchPath, out nint systemHandle))
            {
                return systemHandle;
            }
        }
        
        throw new DllNotFoundException($"Could not load native library '{name}'.");
    }

    private static IEnumerable<string> CandidateNames(string name, int version)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return $"{name}-{version}.dll";
            yield return $"{name}.dll";
        }
        else if (OperatingSystem.IsMacOS())
        {
        }
        else
        {
            yield return $"lib{name}.so.{version}";
            yield return $"lib{name}.so";
        }
    }
}
