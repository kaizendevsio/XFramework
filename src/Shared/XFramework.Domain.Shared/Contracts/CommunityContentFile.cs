using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityContentFile : BaseModel
{
    public Guid ContentId { get; set; }

    public Guid StorageId { get; set; }


    public virtual CommunityContent Content { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}