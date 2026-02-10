using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AtomGateway.Core.Interfaces;

namespace AtomGateway.Api.Hubs;

public class AtomHub : Hub
{
    private readonly ILogger<AtomHub> _logger;
    private readonly ISerialService _serialService;

    public AtomHub(ILogger<AtomHub> logger, ISerialService serialService)
    {
        _logger = logger;
        _serialService = serialService;
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

    // Envia mensagem do frontend para o Atom via Serial
    public async Task SendToAtom(string message)
    {
        try
        {
            _logger.LogInformation("Message from frontend to Atom: {Message}", message);
            
            if (!_serialService.IsConnected)
            {
                _logger.LogWarning("Cannot send to Atom: Serial port not connected");
                await Clients.Caller.SendAsync("Error", "Serial port not connected");
                return;
            }

            // Envia via porta serial para o Atom
            await _serialService.SendAsync(message);
            
            _logger.LogInformation("✅ Message sent to Atom successfully");
            
            // Notifica todos os clientes que a mensagem foi enviada
            await Clients.All.SendAsync("MessageSent", new 
            { 
                message = message, 
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Atom");
            await Clients.Caller.SendAsync("Error", $"Failed to send: {ex.Message}");
        }
    }
}