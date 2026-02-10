namespace AtomGateway.Core.Models;

public class AtomMessage
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string RawData { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string? DecodedData { get; set; }
}

public enum MessageType
{
    Text,
    Binary,
    DtcChunk,
    Photo,
    PassportData,
    Control
}