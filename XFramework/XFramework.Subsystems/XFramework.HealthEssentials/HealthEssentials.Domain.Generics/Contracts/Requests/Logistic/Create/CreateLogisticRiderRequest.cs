using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

public class CreateLogisticRiderRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public short Status { get; set; }
    public string? VehicleType { get; set; }
    public string? PlateNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiry { get; set; }

    public Guid? TypeGuid { get; set; }
    public CreateAddressRequest? Address { get; set; }
}