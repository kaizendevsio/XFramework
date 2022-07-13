using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? PharmacyGuid { get; set; }
    public Guid? PharmacyLocationGuid { get; set; }

    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }

    public GenericStatusType Status { get; set; }

}