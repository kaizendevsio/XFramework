using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyTagRequest : RequestBase
{
    public Guid? PharmacyGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}