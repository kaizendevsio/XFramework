using HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyLocationDocumentRequest : RequestBase
{
    public Guid? PharmacyLocationGuid { get; set; }
    public List<FileUploadRequest>? FileList { get; set; }
}