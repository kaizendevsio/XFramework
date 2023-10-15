namespace XFramework.Domain.Generic.Contracts;

public partial record MessageFile : BaseModel
{
    public Guid MessageId { get; set; }

    public Guid StorageId { get; set; }


    public virtual Message Message { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}