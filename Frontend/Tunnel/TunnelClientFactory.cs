using System.Collections.Concurrent;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by host name.
/// </summary>
internal class TunnelClientFactory : ForwarderHttpClientFactory
{
    private readonly ConcurrentDictionary<string, Channel<Stream>> _clusterConnections = new();

    public Channel<Stream> GetConnectionChannel(string host)
    {
        return _clusterConnections.GetOrAdd(host, _ => Channel.CreateUnbounded<Stream>());
    }

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);

        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            for (int retry = 0; retry < 10; retry++)
            {
                if (_clusterConnections.TryGetValue(context.DnsEndPoint.Host, out var channel))
                {
                    return await channel.Reader.ReadAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(1000, cancellationToken);
            }

            throw new TimeoutException();
        };
    }
}
