namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ChatReferenceDataResponse
{
    public Guid MessageTypeId { get; set; }
    public Guid ThreadTypeId { get; set; }
    public List<ReactionTypeResponse> ReactionTypes { get; set; } = [];
    // Editing rules the client must honour before offering an Edit affordance.
    public int MessageEditWindowMinutes { get; set; } = 15;
    public bool CanEditAnyMessage { get; set; }
}

[MemoryPackable]
public partial record ReactionTypeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
}
