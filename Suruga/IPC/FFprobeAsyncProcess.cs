using Suruga.Primitives;

namespace Suruga.IPC;

internal static class FFprobeAsyncProcess
{
    internal static async Task<Result<AsyncProcess>> StartAsync(string input, CancellationToken token = default)
    {
        string arguments = $"-v error -print_format json -show_format -show_streams -i \"{input}\"";
        return await AsyncProcess.StartAsync("ffprobe", arguments, token);
    }
}
