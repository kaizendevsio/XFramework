﻿using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageThreadMemberRole : BaseModel
{
    public Guid MessageThreadMemberId { get; set; }

    public Guid RoleId { get; set; }

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;

    public virtual IdentityRole Role { get; set; } = null!;
}