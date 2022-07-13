using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryLocationDocumentRequest : RequestBase
{
    public Guid? LaboratoryLocationGuid { get; set; }
}