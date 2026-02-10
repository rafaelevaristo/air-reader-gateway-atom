using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtomGateway.Core.Interfaces;

namespace AtomGateway.Api.Services;

public class SerialPortService : ISerialService, IDisposable
{
    private SerialPort? _serialPort;
    private readonly ILogger<SerialPortService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler<string>? DataReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public bool IsConnected => _serialPort?.IsOpen ?? false;
    public string? CurrentPort => _serialPort?.PortName;

    public SerialPortService(ILogger<SerialPortService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
{
    try
    {
        if (IsConnected)
        {
            _logger.LogWarning("Already connected to {Port}", CurrentPort);
            await Task.CompletedTask; // ← ADICIONE ESTA LINHA
            return true;
        }

        _serialPort = new SerialPort(portName, baudRate)
        {
            Encoding = Encoding.UTF8,
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            NewLine = "\n"
        };

        _serialPort.Open();
        _logger.LogInformation("Connected to {Port} at {BaudRate} baud", portName, baudRate);

        _cancellationTokenSource = new CancellationTokenSource();
        _ = Task.Run(() => ReadDataAsync(_cancellationTokenSource.Token));

        ConnectionStatusChanged?.Invoke(this, true);
        await Task.CompletedTask; // ← ADICIONE ESTA LINHA
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to connect to {Port}", portName);
        await Task.CompletedTask; // ← ADICIONE ESTA LINHA
        return false;
    }
}

    private async Task ReadDataAsync(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_serialPort!.BytesToRead > 0)
                {
                    var data = _serialPort.ReadExisting();
                    buffer.Append(data);

                    var content = buffer.ToString();
                    var lines = content.Split('\n');
                    
                    buffer.Clear();
                    buffer.Append(lines[^1]);

                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            _logger.LogDebug("Serial RX: {Data}", line);
                            DataReceived?.Invoke(this, line);
                        }
                    }
                }
                await Task.Delay(10, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Normal quando não há dados
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading serial data");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public async Task SendAsync(string data)
    {
        if (!IsConnected || _serialPort == null)
        {
            throw new InvalidOperationException("Not connected to serial port");
        }

        try
        {
            await _serialPort.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(data + "\n"));
            await _serialPort.BaseStream.FlushAsync();
            _logger.LogDebug("Serial TX: {Data}", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send data");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
                _logger.LogInformation("Disconnected from {Port}", CurrentPort);
            }

            ConnectionStatusChanged?.Invoke(this, false);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _serialPort?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}