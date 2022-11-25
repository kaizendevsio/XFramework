using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects;

public partial class CommunityIdentityFileEntity
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; } = new List<CommunityIdentityFile>();
}
