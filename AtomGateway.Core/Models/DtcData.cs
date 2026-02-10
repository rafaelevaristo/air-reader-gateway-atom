namespace AtomGateway.Core.Models;

public class DtcData
{
    public string? PassengerName { get; set; }
    public string? PassportNumber { get; set; }
    public string? Nationality { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public byte[]? Photo { get; set; }
    public string? PhotoBase64 => Photo != null ? Convert.ToBase64String(Photo) : null;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public bool IsComplete { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}