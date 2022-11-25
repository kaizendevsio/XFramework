using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class MetaData
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public long? KeyId { get; set; }

    public string? Value { get; set; }

    public string Guid { get; set; } = null!;

    public virtual MetaDataEntity Entity { get; set; } = null!;
}
