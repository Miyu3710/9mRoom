using System.IO;
using System.IO.Pipes;

namespace NineMRoom.Client.App;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\9mRoom.Client";
    private const string PipeName = "9mRoom.client.activate";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly bool _ownsMutex;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        IsFirst = createdNew;

        if (IsFirst)
        {
            _ = Task.Run(ListenAsync);
        }
    }

    public bool IsFirst { get; }

    public event Action? ActivateRequested;

    public static void NotifyExisting()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe);
            writer.WriteLine("show");
            writer.Flush();
        }
        catch
        {
            // 已有实例可能刚好退出，忽略
        }
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cancellation.Token);
                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync(_cancellation.Token);
                if (string.Equals(command, "show", StringComparison.Ordinal))
                {
                    ActivateRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try
                {
                    await Task.Delay(500, _cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // 未持有互斥体时忽略
            }
        }

        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
