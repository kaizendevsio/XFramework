using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

public class DeleteLogisticRiderHandleRequest : RequestBase
{
    public Guid? Guid { get; set; }
}