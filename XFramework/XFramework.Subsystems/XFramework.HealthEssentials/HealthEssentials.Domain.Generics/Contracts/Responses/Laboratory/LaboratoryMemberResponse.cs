using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryMemberResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long CredentialId { get; set; }
    public long LaboratoryId { get; set; }
    public string? Value { get; set; }
    public string Guid { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Status { get; set; }

    public CredentialResponse? Credential { get; set; }
}