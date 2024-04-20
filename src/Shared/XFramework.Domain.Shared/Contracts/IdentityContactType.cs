﻿using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IdentityContactType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<IdentityContact> IdentityContacts { get; set; } = new List<IdentityContact>();
}