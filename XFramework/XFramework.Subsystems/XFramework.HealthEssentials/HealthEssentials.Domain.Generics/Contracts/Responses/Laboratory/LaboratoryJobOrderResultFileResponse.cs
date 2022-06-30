namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderResultFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long StorageFileId { get; set; }
    public Guid? Guid { get; set; }

    public LaboratoryJobOrderResultResponse? LaboratoryJobOrderResult { get; set; }
    
}