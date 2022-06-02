using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class UpdateLaboratoryRequest : RequestBase
{
    public GenericStatusType Status { get; set; }
}