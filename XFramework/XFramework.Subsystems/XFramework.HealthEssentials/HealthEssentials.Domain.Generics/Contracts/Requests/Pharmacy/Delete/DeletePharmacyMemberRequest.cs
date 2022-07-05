using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;

public class DeletePharmacyMemberRequest : RequestBase
{
    public Guid? Guid { get; set; }
}