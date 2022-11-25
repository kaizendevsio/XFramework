﻿using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class LaboratoryJobOrderResultFile
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long LaboratoryJobOrderResultId { get; set; }

    public long StorageFileId { get; set; }

    public string Guid { get; set; } = null!;

    public virtual LaboratoryJobOrderResult LaboratoryJobOrderResult { get; set; } = null!;
}
