using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using AtomGateway.Core.Models;
using PeterO.Cbor;

namespace AtomGateway.Api.Services;

public class DtcProcessingService
{
    private readonly ILogger<DtcProcessingService> _logger;
    private readonly ConcurrentDictionary<string, ChunkedDataBuffer> _buffers = new();

    public DtcProcessingService(ILogger<DtcProcessingService> logger)
    {
        _logger = logger;
    }

    public bool ProcessChunk(string sessionId, byte[] chunk, int chunkIndex, int totalChunks)
    {
        var buffer = _buffers.GetOrAdd(sessionId, _ => new ChunkedDataBuffer
        {
            SessionId = sessionId,
            ExpectedChunks = totalChunks
        });

        while (buffer.Chunks.Count <= chunkIndex)
        {
            buffer.Chunks.Add(Array.Empty<byte>());
        }
        
        buffer.Chunks[chunkIndex] = chunk;
        
        _logger.LogInformation("Chunk {Index}/{Total} received for session {SessionId}", 
            chunkIndex + 1, totalChunks, sessionId);

        return buffer.IsComplete;
    }

    public DtcData? GetCompleteData(string sessionId)
    {
        if (!_buffers.TryGetValue(sessionId, out var buffer) || !buffer.IsComplete)
        {
            return null;
        }

        try
        {
            var completeData = buffer.GetCompleteData();
            var dtcData = ParseDtcData(completeData);
            
            _buffers.TryRemove(sessionId, out _);
            
            return dtcData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse DTC data for session {SessionId}", sessionId);
            return null;
        }
    }

    private DtcData ParseDtcData(byte[] data)
    {
        var cbor = CBORObject.DecodeFromBytes(data);
        
        var dtcData = new DtcData();

        if (cbor.ContainsKey("name"))
            dtcData.PassengerName = cbor["name"].AsString();
            
        if (cbor.ContainsKey("passport"))
            dtcData.PassportNumber = cbor["passport"].AsString();
            
        if (cbor.ContainsKey("nationality"))
            dtcData.Nationality = cbor["nationality"].AsString();
            
        if (cbor.ContainsKey("dob"))
            dtcData.DateOfBirth = DateTime.Parse(cbor["dob"].AsString());
            
        if (cbor.ContainsKey("photo"))
            dtcData.Photo = cbor["photo"].GetByteString();

        foreach (var key in cbor.Keys)
        {
            dtcData.AdditionalData[key.AsString()] = cbor[key].ToJSONString();
        }

        dtcData.IsComplete = true;
        return dtcData;
    }

    public void CleanupOldSessions(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var oldSessions = _buffers
            .Where(kvp => kvp.Value.StartedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in oldSessions)
        {
            _buffers.TryRemove(sessionId, out _);
            _logger.LogInformation("Removed expired session {SessionId}", sessionId);
        }
    }
}