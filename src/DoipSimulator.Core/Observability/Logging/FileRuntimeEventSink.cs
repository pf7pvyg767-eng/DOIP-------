using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Observability.Logging;

public sealed class FileRuntimeEventSink : IRuntimeEventSink, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public FileRuntimeEventSink(string path)
    {
        this.path = path;
    }

    public RuntimeEventPublishError? LastError { get; private set; }

    public async ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await writeLock.WaitAsync(cancellationToken);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var line = JsonSerializer.Serialize(runtimeEvent, JsonOptions) + Environment.NewLine;
                await using var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    useAsync: true);
                await using var writer = new StreamWriter(stream, Utf8NoBom);
                await writer.WriteAsync(line.AsMemory(), cancellationToken);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LastError = new RuntimeEventPublishError(
                DateTimeOffset.UtcNow,
                nameof(FileRuntimeEventSink),
                exception.Message,
                exception);
        }
    }

    public ValueTask DisposeAsync()
    {
        writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
