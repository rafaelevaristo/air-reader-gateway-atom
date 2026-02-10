namespace AtomGateway.Core.Interfaces;

public interface ISerialService
{
    event EventHandler<string>? DataReceived;
    event EventHandler<bool>? ConnectionStatusChanged;
    
    Task<bool> ConnectAsync(string portName, int baudRate = 115200);
    Task DisconnectAsync();
    Task SendAsync(string data);
    bool IsConnected { get; }
    string? CurrentPort { get; }
}