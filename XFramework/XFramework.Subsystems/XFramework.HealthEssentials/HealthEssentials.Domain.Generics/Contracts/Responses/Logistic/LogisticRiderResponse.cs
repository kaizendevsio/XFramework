using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticRiderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public string Guid { get; set; } = null!;

    public CredentialResponse? Credential { get; set; }
}