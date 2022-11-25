using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticRiderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public Guid? Guid { get; set; }
    public string? CredentialGuid { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public short Status { get; set; }
    public string? VehicleType { get; set; }
    public string? PlateNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public Guid? TypeGuid { get; set; }

    public CredentialResponse? Credential { get; set; }
    public List<StorageFileResponse>? Files { get; set; }
}