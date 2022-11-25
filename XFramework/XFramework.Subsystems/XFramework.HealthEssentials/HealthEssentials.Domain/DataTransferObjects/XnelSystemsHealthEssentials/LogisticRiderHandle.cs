using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class LogisticRiderHandle
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long LogisticId { get; set; }

    public long LogisticRiderId { get; set; }

    public short Status { get; set; }

    public string Guid { get; set; } = null!;

    public virtual Logistic Logistic { get; set; } = null!;

    public virtual LogisticRider LogisticRider { get; set; } = null!;
}
