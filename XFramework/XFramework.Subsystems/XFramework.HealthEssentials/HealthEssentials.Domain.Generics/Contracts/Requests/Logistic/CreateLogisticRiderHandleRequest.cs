using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

public class CreateLogisticRiderHandleRequest : RequestBase
{
    public Guid? RiderGuid { get; set; }
    public Guid? LogisticGuid { get; set; }
    public GenericStatusType Status { get; set; }
}