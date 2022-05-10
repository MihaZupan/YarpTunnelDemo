using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;

public class TunnelConnectionListenerFactory : IConnectionListenerFactory
{
    private readonly TunnelOptions _options;
    private readonly ILogger<TunnelConnectionListenerFactory> _logger;

    public TunnelConnectionListenerFactory(ILogger<TunnelConnectionListenerFactory> logger, IOptions<TunnelOptions> options)
    {
        _options = options.Value;
        _logger = logger;
    }

    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        return new(new TunnelConnectionListener(_logger, _options, endpoint));
    }
}