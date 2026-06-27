namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ThreadMemberResponse
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public string Alias { get; set; } = null!;
    public short Status { get; set; }
    public DateTime JoinedAt { get; set; }
}
