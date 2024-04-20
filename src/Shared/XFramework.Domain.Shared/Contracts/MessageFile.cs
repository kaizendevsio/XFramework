using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageFile : BaseModel
{
    public Guid MessageId { get; set; }

    public Guid StorageId { get; set; }


    public virtual Message Message { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}