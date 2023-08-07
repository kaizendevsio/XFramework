namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityContentFile
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid ContentId { get; set; }

    public Guid StorageId { get; set; }

    
    public virtual CommunityContent Content { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}
