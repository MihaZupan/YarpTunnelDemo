using Microsoft.Net.Http.Headers;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLettuceEncrypt();

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();

var app = builder.Build();

//app.UseWebSockets();

app.MapReverseProxy();

// Auth can be added to this endpoint and we can restrict it to certain points
// to avoid exteranl traffic hitting it
app.MapHttp2Tunnel("/x-tunnel-connect-h2")
    .Add(endpointBuilder =>
    {
        var previous = endpointBuilder.RequestDelegate;
        endpointBuilder.RequestDelegate = async context =>
        {
            if (!context.Request.Headers.TryGetValue(HeaderNames.Authorization, out var token) ||
                token.Count != 1 ||
                app.Configuration["X-Tunnel-Token"] is not string configToken ||
                !Equals(token.ToString(), configToken))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            if (previous is not null)
            {
                await previous(context);
            }
        };
    });

app.Run();

static bool Equals(string a, string b)
{
    if (a.Length != b.Length) return false;

    int diff = 0;

    for (int i = 0; i < a.Length; i++)
    {
        diff |= a[i] ^ b[i];
    }

    return diff == 0;
}