using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

public class GetLaboratoryLocationDocumentListRequest : RequestBase
{
    public Guid? LaboratoryLocationGuid { get; set; }
}