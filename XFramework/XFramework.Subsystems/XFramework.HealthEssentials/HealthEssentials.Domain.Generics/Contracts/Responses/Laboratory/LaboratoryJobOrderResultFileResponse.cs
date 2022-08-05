using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderResultFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }

    public LaboratoryJobOrderResultResponse? LaboratoryJobOrderResult { get; set; }
    public StorageFileResponse? StorageFile { get; set; }
    
}