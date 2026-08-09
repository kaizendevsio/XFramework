namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ValidateIdentitySessionResponse
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid IdentityId { get; set; }
    public Guid SessionId { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<string> Capabilities { get; set; } = [];
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string GenerationId { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsValid { get; set; }
}
