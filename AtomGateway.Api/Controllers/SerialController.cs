using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AtomGateway.Core.Interfaces;

namespace AtomGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SerialController : ControllerBase
{
    private readonly ISerialService _serialService;
    private readonly ILogger<SerialController> _logger;

    public SerialController(ISerialService serialService, ILogger<SerialController> logger)
    {
        _serialService = serialService;
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            IsConnected = _serialService.IsConnected,
            Port = _serialService.CurrentPort
        });
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        if (!_serialService.IsConnected)
        {
            return BadRequest("Serial port not connected");
        }

        try
        {
            await _serialService.SendAsync(request.Message);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("ports")]
    public IActionResult GetAvailablePorts()
    {
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        return Ok(ports);
    }
}

public record SendMessageRequest(string Message);