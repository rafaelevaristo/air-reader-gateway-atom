namespace AtomGateway.Core.Models;

public class ChunkedDataBuffer
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public List<byte[]> Chunks { get; set; } = new();
    public int ExpectedChunks { get; set; }
    public int ReceivedChunks => Chunks.Count;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool IsComplete => ReceivedChunks == ExpectedChunks && ExpectedChunks > 0;
    
    public byte[] GetCompleteData()
    {
        if (!IsComplete) throw new InvalidOperationException("Data not complete");
        return Chunks.SelectMany(c => c).ToArray();
    }
}