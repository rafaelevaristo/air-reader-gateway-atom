using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AtomGateway.Api.Hubs;

public class AtomHub : Hub
{
    private readonly ILogger<AtomHub> _logger;

    public AtomHub(ILogger<AtomHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendToAtom(string message)
    {
        _logger.LogInformation("Message from frontend to Atom: {Message}", message);
        await Clients.Others.SendAsync("MessageToAtom", message);
    }
}