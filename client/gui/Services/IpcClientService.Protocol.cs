using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

public sealed partial class IpcClientService
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(5);

    private async Task<IpcResponseDto> SendRequestAsync(
        IpcRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan? timeoutOverride = null)
    {
        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            Exception? lastException = null;
            TimeSpan requestTimeout = timeoutOverride ?? DefaultRequestTimeout;

            foreach (string pipeName in _pipeCandidates)
            {
                try
                {
                    return await SendRequestOnPipeAsync(pipeName, request, cancellationToken, requestTimeout);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            throw lastException ?? new InvalidOperationException("No IPC pipe reachable");
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static async Task<IpcResponseDto> SendRequestOnPipeAsync(
        string pipeName,
        IpcRequestDto request,
        CancellationToken cancellationToken,
        TimeSpan timeoutWindow)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await ConnectPipeWithTimeoutAsync(pipe, pipeName, timeoutWindow, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutWindow);

        using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

        string json = JsonSerializer.Serialize(request);
        await writer.WriteLineAsync(json);

        string? responseLine = await reader.ReadLineAsync(timeout.Token);
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            throw new InvalidOperationException("IPC returned empty response");
        }

        IpcResponseDto? response = JsonSerializer.Deserialize<IpcResponseDto>(responseLine);
        if (response is null)
        {
            throw new InvalidOperationException("IPC response parse failed");
        }

        return response;
    }

    private static async Task ConnectPipeWithTimeoutAsync(
        NamedPipeClientStream pipe,
        string pipeName,
        TimeSpan timeoutWindow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int timeoutMs = Math.Max(1, (int)Math.Ceiling(timeoutWindow.TotalMilliseconds));

        try
        {
            await Task.Run(() => pipe.Connect(timeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"IPC connect timeout on pipe '{pipeName}'.");
        }
    }
}
