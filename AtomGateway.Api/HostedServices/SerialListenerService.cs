using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using AtomGateway.Core.Interfaces;
using AtomGateway.Core.Models;
using AtomGateway.Api.Hubs;
using AtomGateway.Api.Services;

namespace AtomGateway.Api.HostedServices;

public class SerialListenerService : BackgroundService
{
    private readonly ISerialService _serialService;
    private readonly IHubContext<AtomHub> _hubContext;
    private readonly DtcProcessingService _dtcService;
    private readonly ILogger<SerialListenerService> _logger;
    private readonly IConfiguration _configuration;

    public SerialListenerService(
        ISerialService serialService,
        IHubContext<AtomHub> hubContext,
        DtcProcessingService dtcService,
        ILogger<SerialListenerService> logger,
        IConfiguration configuration)
    {
        _serialService = serialService;
        _hubContext = hubContext;
        _dtcService = dtcService;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var portName = _configuration["SerialPort:PortName"] ?? "COM3";
        var baudRate = _configuration.GetValue<int>("SerialPort:BaudRate", 115200);

        _logger.LogInformation("Starting Serial Listener on {Port} @ {BaudRate}", portName, baudRate);

        _serialService.DataReceived += OnDataReceived;
        _serialService.ConnectionStatusChanged += OnConnectionStatusChanged;

        var connected = await _serialService.ConnectAsync(portName, baudRate);
        
        if (!connected)
        {
            _logger.LogError("Failed to connect to serial port. Will retry...");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _dtcService.CleanupOldSessions(TimeSpan.FromMinutes(10));
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        await _serialService.DisconnectAsync();
    }

    private async void OnDataReceived(object? sender, string data)
    {
        try
        {
            var message = new AtomMessage
            {
                RawData = data,
                Type = DetectMessageType(data)
            };

            _logger.LogInformation("Received from Atom: {Type} - {Data}", message.Type, data);

            await _hubContext.Clients.All.SendAsync("DataReceived", message);

            switch (message.Type)
            {
                case MessageType.DtcChunk:
                    await ProcessDtcChunk(data);
                    break;
                    
                case MessageType.Text:
                    message.DecodedData = data;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data");
        }
    }

    private async Task ProcessDtcChunk(string data)
    {
        var parts = data.Split(':');
        if (parts.Length != 5 || parts[0] != "DTC") return;

        var sessionId = parts[1];
        var chunkIndex = int.Parse(parts[2]);
        var totalChunks = int.Parse(parts[3]);
        var chunkData = Convert.FromBase64String(parts[4]);

        var isComplete = _dtcService.ProcessChunk(sessionId, chunkData, chunkIndex, totalChunks);

        if (isComplete)
        {
            var dtcData = _dtcService.GetCompleteData(sessionId);
            if (dtcData != null)
            {
                _logger.LogInformation("DTC data complete for {Name}", dtcData.PassengerName);
                await _hubContext.Clients.All.SendAsync("IdentityReceived", dtcData);
            }
        }
    }

    private MessageType DetectMessageType(string data)
    {
        if (data.StartsWith("DTC:")) return MessageType.DtcChunk;
        if (data.StartsWith("PHOTO:")) return MessageType.Photo;
        if (data.StartsWith("PASSPORT:")) return MessageType.PassportData;
        return MessageType.Text;
    }

    private async void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        _logger.LogInformation("Serial port {Status}", isConnected ? "CONNECTED" : "DISCONNECTED");
        await _hubContext.Clients.All.SendAsync("SerialStatus", isConnected);
    }
}