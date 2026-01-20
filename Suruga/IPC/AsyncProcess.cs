using System.Diagnostics;
using Suruga.Primitives;

namespace Suruga.IPC;

internal sealed class AsyncProcess : IDisposable
{
    internal Stream StandardOutput => _process.StandardOutput.BaseStream;

    internal Stream StandardError => _process.StandardError.BaseStream;

    internal Stream StandardInput => _process.StandardInput.BaseStream;

    private readonly Process _process;
    private readonly TaskCompletionSource<int> _exitTcs = new();

    private bool _isDisposed;

    private AsyncProcess(Process process)
    {
        _process = process;
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => _exitTcs.TrySetResult(_process.ExitCode);
    }

    internal static async Task<Result<AsyncProcess>> StartAsync(string fileName, string arguments, CancellationToken token = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return Result<AsyncProcess>.Failure($"Failed to start process '{fileName}'.");
            }

            AsyncProcess asyncProcess = new(process);

            // Wait a bit to ensure the process started successfully.
            await Task.Delay(TimeSpan.FromMilliseconds(50), token);

            return !process.HasExited || process.ExitCode == 0
                ? Result<AsyncProcess>.Success(asyncProcess)
                : Result<AsyncProcess>.Failure($"Process exited immediately with code '{process.ExitCode}'.");
        }
        catch (Exception ex)
        {
            process.Kill(true);
            process.Dispose();

            return Result<AsyncProcess>.Failure(ex);
        }
    }

    internal async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Process did not exit within {timeout.TotalSeconds} seconds");
        }
    }
    
    private async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await using CancellationTokenRegistration registration = cancellationToken.Register(() => _exitTcs.TrySetCanceled(cancellationToken));
        return await _exitTcs.Task;
    }

    private void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        Kill();
        _process.Dispose();
    }
}
