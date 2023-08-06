namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryJobOrderResultFile
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid LaboratoryJobOrderResultId { get; set; }

    public Guid StorageFileId { get; set; }

    
    public virtual LaboratoryJobOrderResult LaboratoryJobOrderResult { get; set; } = null!;
}
