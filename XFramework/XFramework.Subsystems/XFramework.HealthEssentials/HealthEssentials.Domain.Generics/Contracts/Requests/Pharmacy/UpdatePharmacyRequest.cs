using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;

public class UpdatePharmacyRequest : RequestBase
{
    public GenericStatusType Status { get; set; }
}