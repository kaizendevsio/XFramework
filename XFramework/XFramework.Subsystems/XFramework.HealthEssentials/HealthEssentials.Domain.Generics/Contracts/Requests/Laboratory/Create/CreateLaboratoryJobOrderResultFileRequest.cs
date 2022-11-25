namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryJobOrderResultFileRequest : RequestBase
{
    public Guid? LaboratoryJobOrderResultGuid { get; set; }
    public Guid? StorageFileGuid { get; set; }
}