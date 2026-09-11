namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessageReactionSummaryResponse
{
    public Guid TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public Guid? MyReactionId { get; set; }
}
