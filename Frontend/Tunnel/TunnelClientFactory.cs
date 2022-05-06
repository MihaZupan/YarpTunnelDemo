using System.Collections.Concurrent;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by host name.
/// </summary>
internal class TunnelClientFactory : IForwarderHttpClientFactory
{
    private readonly ConcurrentDictionary<string, Channel<Stream>> _clusterConnections = new();
    private readonly ForwarderHttpClientFactory _httpFactory = new();
    private readonly TunnelForwarderClientFactory _tunnelFactory;

    public TunnelClientFactory()
    {
        _tunnelFactory = new TunnelForwarderClientFactory(this);
    }

    public Channel<Stream> GetConnectionChannel(string host)
    {
        return _clusterConnections.GetOrAdd(host, _ => Channel.CreateUnbounded<Stream>());
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        if (context.OldClient is not null && context.OldConfig == context.NewConfig)
        {
            return context.OldClient;
        }

        return new HttpMessageInvoker(new TunnelHttpHandler(
            _httpFactory.CreateClient(context),
            _tunnelFactory.CreateClient(context)));
    }

    private sealed class TunnelForwarderClientFactory : ForwarderHttpClientFactory
    {
        private readonly TunnelClientFactory _tunnelFactory;

        public TunnelForwarderClientFactory(TunnelClientFactory tunnelFactory)
        {
            _tunnelFactory = tunnelFactory;
        }

        protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
        {
            base.ConfigureHandler(context, handler);

            handler.ConnectCallback = (context, cancellation) =>
            {
                var channel = _tunnelFactory.GetConnectionChannel(context.InitialRequestMessage.RequestUri!.IdnHost);
                return channel.Reader.ReadAsync(cancellation);
            };
        }
    }

    private sealed class TunnelHttpHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _httpHandler;
        private readonly HttpMessageInvoker _tunnelHandler;

        public TunnelHttpHandler(HttpMessageInvoker httpHandler, HttpMessageInvoker tunnelHandler)
        {
            _httpHandler = httpHandler;
            _tunnelHandler = tunnelHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Scheme == "tunnel")
            {
                request.RequestUri = new Uri(string.Concat("http", request.RequestUri.AbsoluteUri.AsSpan("tunnel".Length)), new UriCreationOptions
                {
                    DangerousDisablePathAndQueryCanonicalization = true
                });

                return _tunnelHandler.SendAsync(request, cancellationToken);
            }
            else
            {
                return _httpHandler.SendAsync(request, cancellationToken);
            }
        }
    }
}
