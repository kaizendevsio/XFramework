using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class IdentityRoleEntityGroup
{
    public IdentityRoleEntityGroup()
    {
        IdentityRoleEntities = new HashSet<IdentityRoleEntity>();
    }

    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Guid { get; set; }

    public virtual ICollection<IdentityRoleEntity> IdentityRoleEntities { get; set; }
}