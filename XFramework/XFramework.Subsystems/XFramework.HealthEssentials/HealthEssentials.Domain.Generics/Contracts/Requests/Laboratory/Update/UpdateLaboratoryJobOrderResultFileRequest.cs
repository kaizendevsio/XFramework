namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryJobOrderResultFileRequest : RequestBase
{
    public Guid? LaboratoryJobOrderResultGuid { get; set; }
    public Guid? StorageFileGuid { get; set; }
}