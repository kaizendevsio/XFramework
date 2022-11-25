﻿using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class LaboratoryJobOrderResult
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long LaboratoryJobOrderId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Guid { get; set; } = null!;

    public virtual LaboratoryJobOrder LaboratoryJobOrder { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrderResultFile> LaboratoryJobOrderResultFiles { get; } = new List<LaboratoryJobOrderResultFile>();
}
