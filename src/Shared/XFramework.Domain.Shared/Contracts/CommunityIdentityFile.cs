using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityIdentityFile : BaseModel
{
    public Guid IdentityId { get; set; }

    public Guid StorageId { get; set; }

    public Guid TypeId { get; set; }

    public virtual CommunityIdentityFileType Type { get; set; } = null!;

    public virtual CommunityIdentity Identity { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}