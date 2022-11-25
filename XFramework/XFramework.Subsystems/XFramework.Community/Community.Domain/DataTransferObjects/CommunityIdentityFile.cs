using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects;

public partial class CommunityIdentityFile
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long IdentityId { get; set; }

    public long StorageId { get; set; }

    public string Guid { get; set; } = null!;

    public long EntityId { get; set; }

    public virtual CommunityIdentityFileEntity Entity { get; set; } = null!;

    public virtual CommunityIdentity Identity { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}
