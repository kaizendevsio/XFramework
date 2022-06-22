using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyMemberResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long CredentialId { get; set; }
    public long PharmacyId { get; set; }
    public string? Value { get; set; }
    public string? Guid { get; set; }
    public string? Name { get; set; }
    public int Status { get; set; }

    public CredentialResponse? Credential { get; set; }
}