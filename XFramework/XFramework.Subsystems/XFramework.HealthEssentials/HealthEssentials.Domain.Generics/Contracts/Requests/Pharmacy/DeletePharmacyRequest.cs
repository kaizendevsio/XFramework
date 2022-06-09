using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;

public class DeletePharmacyRequest : RequestBase
{
    public Guid? Guid { get; set; }
}