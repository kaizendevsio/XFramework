using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

public class DeleteLogisticRequest : RequestBase
{
    public Guid? Guid { get; set; }
}