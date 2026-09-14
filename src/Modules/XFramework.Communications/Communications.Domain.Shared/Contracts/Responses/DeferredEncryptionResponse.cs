namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record DeferredEncryptionResponse
{
    public List<DeferredEncryptedMessage> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

[MemoryPackable]
public partial record DeferredEncryptedMessage
{
    public Guid ThreadId { get; set; }
    public ThreadMessageItemResponse Message { get; set; } = new();
    public string EnvelopeHash { get; set; } = "";
    public List<Guid> PendingCredentialIds { get; set; } = [];
}
