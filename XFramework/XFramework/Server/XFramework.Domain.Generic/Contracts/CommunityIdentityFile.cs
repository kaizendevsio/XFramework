namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityIdentityFile
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid IdentityId { get; set; }

    public Guid StorageId { get; set; }
    
    public Guid TypeId { get; set; }

    public virtual CommunityIdentityFileType Type { get; set; } = null!;

    public virtual CommunityIdentity Identity { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}
