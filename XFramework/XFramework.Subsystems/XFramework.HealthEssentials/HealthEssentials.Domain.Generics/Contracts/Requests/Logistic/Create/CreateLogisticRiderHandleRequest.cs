using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

public class CreateLogisticRiderHandleRequest : RequestBase
{
    public Guid? RiderGuid { get; set; }
    public Guid? LogisticGuid { get; set; }
    public GenericStatusType Status { get; set; }
}