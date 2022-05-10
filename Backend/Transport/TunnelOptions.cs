public class TunnelOptions
{
    public int MaxConnectionCount { get; set; } = 25;

    public TransportType Transport { get; set; } = TransportType.HTTP2;

    public string? AuthHeaderValue { get; set; }
}

public enum TransportType
{
    WebSockets,
    HTTP2
}