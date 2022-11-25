using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects;

public partial class IdentityRoleEntityGroup
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public virtual ICollection<IdentityRoleEntity> IdentityRoleEntities { get; } = new List<IdentityRoleEntity>();
}
