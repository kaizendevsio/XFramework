using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

public class UpdateLogisticRiderHandleRequest : RequestBase
{
    public Guid? RiderGuid { get; set; }
    public Guid? LogisticGuid { get; set; }
    public GenericStatusType Status { get; set; }
}