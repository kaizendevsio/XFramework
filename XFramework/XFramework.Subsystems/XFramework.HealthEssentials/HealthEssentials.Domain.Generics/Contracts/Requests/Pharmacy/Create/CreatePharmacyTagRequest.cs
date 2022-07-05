using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyTagRequest : RequestBase
{
    public PharmacyTag Tag { get; set; }
    public bool IsEnabled { get; set; }
}