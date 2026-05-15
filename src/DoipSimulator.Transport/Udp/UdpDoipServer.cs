using System.Net;
using System.Net.Sockets;

namespace DoipSimulator.Transport.Udp;

public sealed record UdpDoipServerOptions(
    IPAddress BindAddress,
    int Port,
    bool VehicleAnnouncementEnabled = false,
    TimeSpan? VehicleAnnouncementInterval = null,
    IPEndPoint? VehicleAnnouncementTarget = null);

public sealed class UdpDoipServer : IAsyncDisposable
{
    private readonly UdpDoipServerOptions options;
    private readonly IDoipUdpHandler handler;
    private readonly VehicleIdentificationUdpHandler? announcementHandler;
    private CancellationTokenSource? shutdown;
    private UdpClient? udpClient;
    private Task? receiveTask;
    private Task? announcementTask;

    public UdpDoipServer(UdpDoipServerOptions options, IDoipUdpHandler handler)
    {
        this.options = options;
        this.handler = handler;
        announcementHandler = handler as VehicleIdentificationUdpHandler;
    }

    public int BoundPort
    {
        get
        {
            if (udpClient?.Client.LocalEndPoint is IPEndPoint endpoint)
            {
                return endpoint.Port;
            }

            return options.Port;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (udpClient is not null)
        {
            return Task.CompletedTask;
        }

        shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            udpClient = new UdpClient(new IPEndPoint(options.BindAddress, options.Port))
            {
                EnableBroadcast = true,
            };
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException(
                $"Failed to bind DoIP UDP endpoint {options.BindAddress}:{options.Port}: {exception.Message}",
                exception);
        }

        receiveTask = Task.Run(() => ReceiveLoopAsync(shutdown.Token), CancellationToken.None);

        if (options.VehicleAnnouncementEnabled && announcementHandler is not null)
        {
            announcementTask = Task.Run(() => AnnouncementLoopAsync(shutdown.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (shutdown is null)
        {
            return;
        }

        await shutdown.CancelAsync();
        udpClient?.Dispose();

        var tasks = new[] { receiveTask, announcementTask }.Where(task => task is not null).Cast<Task>();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        shutdown.Dispose();
        shutdown = null;
        udpClient = null;
        receiveTask = null;
        announcementTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && udpClient is not null)
        {
            UdpReceiveResult result;
            try
            {
                result = await udpClient.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var outbound = await handler.HandleAsync(
                new InboundDatagram(result.Buffer, result.RemoteEndPoint),
                cancellationToken);

            foreach (var datagram in outbound)
            {
                await udpClient.SendAsync(datagram.Payload, datagram.TargetEndpoint, cancellationToken);
            }
        }
    }

    private async Task AnnouncementLoopAsync(CancellationToken cancellationToken)
    {
        var interval = options.VehicleAnnouncementInterval ?? TimeSpan.FromSeconds(1);
        var target = options.VehicleAnnouncementTarget ?? new IPEndPoint(IPAddress.Broadcast, options.Port);

        while (!cancellationToken.IsCancellationRequested && udpClient is not null && announcementHandler is not null)
        {
            var payload = announcementHandler.CreateAnnouncementDatagram();
            await udpClient.SendAsync(payload, target, cancellationToken);
            await announcementHandler.PublishAnnouncementAsync(target, cancellationToken);
            await Task.Delay(interval, cancellationToken);
        }
    }
}
