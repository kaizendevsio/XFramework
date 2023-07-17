﻿using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class IdentityContactEntity
{
    public IdentityContactEntity()
    {
        IdentityContacts = new HashSet<IdentityContact>();
    }

    public long Id { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
    public string Guid { get; set; }

    public virtual ICollection<IdentityContact> IdentityContacts { get; set; }
}