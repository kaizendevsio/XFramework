using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class ScheduleTag
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long ScheduleId { get; set; }

    public string? Value { get; set; }

    public string Guid { get; set; } = null!;

    public long TagId { get; set; }

    public virtual Schedule Schedule { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
