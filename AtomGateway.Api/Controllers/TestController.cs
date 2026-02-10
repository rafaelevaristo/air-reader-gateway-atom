using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AtomGateway.Api.Hubs;
using AtomGateway.Api.Services;
using AtomGateway.Core.Models;
using PeterO.Cbor;
using System;
using System.Threading.Tasks;

namespace AtomGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IHubContext<AtomHub> _hubContext;
    private readonly DtcProcessingService _dtcService;

    public TestController(IHubContext<AtomHub> hubContext, DtcProcessingService dtcService)
    {
        _hubContext = hubContext;
        _dtcService = dtcService;
    }

    [HttpGet("send-fake-dtc")]
    public async Task<IActionResult> SendFakeDtc()
    {
        // Criar dados CBOR válidos
        var cborData = CBORObject.NewMap();
        cborData.Add("name", "John Doe");
        cborData.Add("passport", "US123456789");
        cborData.Add("nationality", "USA");
        cborData.Add("dob", "1985-05-15");
        
        var cborBytes = cborData.EncodeToBytes();
        var base64 = Convert.ToBase64String(cborBytes);

        // Simular recebimento de chunk único
        var sessionId = "FAKE" + DateTime.Now.Ticks;
        _dtcService.ProcessChunk(sessionId, cborBytes, 0, 1);
        
        var dtcData = _dtcService.GetCompleteData(sessionId);
        
        if (dtcData != null)
        {
            await _hubContext.Clients.All.SendAsync("IdentityReceived", dtcData);
            return Ok(new { success = true, data = dtcData, base64 });
        }

        return BadRequest("Failed to process");
    }
}