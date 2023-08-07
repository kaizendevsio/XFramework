namespace XFramework.Domain.Generic.Contracts;

public partial class MessageFile
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid MessageId { get; set; }

    public Guid StorageId { get; set; }

    
    public virtual Message Message { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}
