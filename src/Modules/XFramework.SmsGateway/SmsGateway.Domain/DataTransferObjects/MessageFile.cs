using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects;

public partial class MessageFile
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long MessageId { get; set; }

    public long StorageId { get; set; }

    public string Guid { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}
