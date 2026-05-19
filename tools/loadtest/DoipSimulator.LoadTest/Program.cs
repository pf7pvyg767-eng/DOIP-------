using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

var options = LoadTestOptions.Parse(args);
var startedAt = Stopwatch.StartNew();
var connections = new List<DoipConnection>();
var failures = 0;
var successes = 0;
var total = 0;
string? lastFailure = null;

try
{
    for (var index = 0; index < options.Connections; index++)
    {
        var testerAddress = options.IncrementTesterAddress
            ? checked((ushort)(options.TesterAddress + index))
            : options.TesterAddress;
        connections.Add(await DoipConnection.ConnectAsync(options, testerAddress));
    }

    var tasks = new List<Task>();
    var deadline = Stopwatch.StartNew();
    var interval = TimeSpan.FromSeconds(1d / options.RequestsPerSecond);
    var nextDue = TimeSpan.Zero;
    var cursor = 0;

    while (deadline.Elapsed < options.Duration)
    {
        var delay = nextDue - deadline.Elapsed;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        var connection = connections[cursor++ % connections.Count];
        Interlocked.Increment(ref total);
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                if (await connection.SendReadDidAsync(options))
                {
                    Interlocked.Increment(ref successes);
                }
                else
                {
                    Interlocked.Increment(ref failures);
                    lastFailure = "Unexpected diagnostic response.";
                }
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failures);
                lastFailure = exception.Message;
            }
        }));

        nextDue += interval;
    }

    await Task.WhenAll(tasks);
}
finally
{
    foreach (var connection in connections)
    {
        connection.Dispose();
    }
}

var duration = startedAt.Elapsed.TotalSeconds;
var result = new
{
    host = options.Host,
    port = options.Port,
    targetConnections = options.Connections,
    establishedConnections = connections.Count,
    targetRequestsPerSecond = options.RequestsPerSecond,
    durationSeconds = Math.Round(duration, 3),
    totalRequests = total,
    successfulResponses = successes,
    failedResponses = failures,
    lastFailure,
    successRate = total == 0 ? 0 : Math.Round((double)successes / total, 4),
    achievedRequestsPerSecond = duration <= 0 ? 0 : Math.Round(total / duration, 3),
};

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return connections.Count == options.Connections && failures == 0 ? 0 : 1;

internal sealed class DoipConnection : IDisposable
{
    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ushort testerAddress;

    private DoipConnection(TcpClient client, ushort testerAddress)
    {
        this.client = client;
        stream = client.GetStream();
        this.testerAddress = testerAddress;
    }

    public static async Task<DoipConnection> ConnectAsync(LoadTestOptions options, ushort testerAddress)
    {
        var client = new TcpClient();
        using var timeout = new CancellationTokenSource(options.Timeout);
        await client.ConnectAsync(options.Host, options.Port, timeout.Token);
        var connection = new DoipConnection(client, testerAddress);
        await connection.ActivateAsync(options, timeout.Token);
        return connection;
    }

    public async Task<bool> SendReadDidAsync(LoadTestOptions options)
    {
        using var timeout = new CancellationTokenSource(options.Timeout);
        await gate.WaitAsync(timeout.Token);
        try
        {
            var payload = new byte[7];
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), testerAddress);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), options.EcuAddress);
            payload[4] = 0x22;
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(5, 2), options.Did);
            await WriteFrameAsync(0x8001, payload, timeout.Token);
            var response = await ReadFrameAsync(timeout.Token);
            return response.PayloadType == 0x8001 &&
                response.Payload.Length >= 5 &&
                response.Payload[4] is 0x62 or 0x7F;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        gate.Dispose();
        stream.Dispose();
        client.Dispose();
    }

    private async Task ActivateAsync(LoadTestOptions options, CancellationToken cancellationToken)
    {
        var payload = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), testerAddress);
        payload[2] = 0x00;
        await WriteFrameAsync(0x0005, payload, cancellationToken);
        var response = await ReadFrameAsync(cancellationToken);
        if (response.PayloadType != 0x0006 || response.Payload.Length < 5 || response.Payload[4] != 0x10)
        {
            throw new InvalidOperationException($"Routing activation failed for tester 0x{testerAddress:X4}.");
        }
    }

    private async Task WriteFrameAsync(ushort payloadType, byte[] payload, CancellationToken cancellationToken)
    {
        var frame = new byte[8 + payload.Length];
        frame[0] = 0x02;
        frame[1] = 0xFD;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), payloadType);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4, 4), checked((uint)payload.Length));
        payload.CopyTo(frame.AsSpan(8));
        await stream.WriteAsync(frame, cancellationToken);
    }

    private async Task<DoipFrame> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(8, cancellationToken);
        var payloadType = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4)));
        var payload = length == 0 ? [] : await ReadExactAsync(length, cancellationToken);
        return new DoipFrame(payloadType, payload);
    }

    private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed while reading a DoIP frame.");
            }

            offset += read;
        }

        return buffer;
    }
}

internal sealed record DoipFrame(ushort PayloadType, byte[] Payload);

internal sealed record LoadTestOptions(
    string Host,
    int Port,
    int Connections,
    int RequestsPerSecond,
    TimeSpan Duration,
    ushort TesterAddress,
    bool IncrementTesterAddress,
    ushort EcuAddress,
    ushort Did,
    TimeSpan Timeout)
{
    public static LoadTestOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var incrementTesterAddress = false;
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (key == "--increment-tester-address")
            {
                incrementTesterAddress = true;
                continue;
            }

            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Invalid argument: {key}");
            }

            values[key[2..]] = args[++index];
        }

        return new LoadTestOptions(
            values.GetValueOrDefault("host", "127.0.0.1"),
            int.Parse(values.GetValueOrDefault("port", "13400")),
            int.Parse(values.GetValueOrDefault("connections", "20")),
            int.Parse(values.GetValueOrDefault("rps", "200")),
            TimeSpan.FromSeconds(int.Parse(values.GetValueOrDefault("duration-seconds", "10"))),
            ParseHexUInt16(values.GetValueOrDefault("tester-address", "0x0E80")),
            incrementTesterAddress,
            ParseHexUInt16(values.GetValueOrDefault("ecu-address", "0x0E00")),
            ParseHexUInt16(values.GetValueOrDefault("did", "0xF190")),
            TimeSpan.FromMilliseconds(int.Parse(values.GetValueOrDefault("timeout-ms", "2000"))));
    }

    private static ushort ParseHexUInt16(string value)
    {
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return Convert.ToUInt16(normalized, 16);
    }
}
