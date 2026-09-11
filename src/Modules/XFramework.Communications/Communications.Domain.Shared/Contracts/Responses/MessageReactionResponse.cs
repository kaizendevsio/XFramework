namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessageReactionResponse
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid TypeId { get; set; }
    public Guid MemberId { get; set; }
    public Guid CredentialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
