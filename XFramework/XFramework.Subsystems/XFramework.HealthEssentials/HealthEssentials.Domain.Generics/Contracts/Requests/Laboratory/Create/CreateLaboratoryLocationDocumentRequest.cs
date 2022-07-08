using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryLocationDocumentRequest : RequestBase
{
    public Guid? LaboratoryLocationGuid { get; set; }    
    public List<FileUploadRequest>? FileList { get; set; }
}