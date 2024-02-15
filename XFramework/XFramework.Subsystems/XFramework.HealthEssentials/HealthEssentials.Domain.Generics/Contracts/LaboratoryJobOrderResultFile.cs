namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryJobOrderResultFile : BaseModel
{
    public Guid LaboratoryJobOrderResultId { get; set; }

    public Guid StorageFileId { get; set; }


    public virtual LaboratoryJobOrderResult LaboratoryJobOrderResult { get; set; } = null!;
}