using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryTag
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LaboratoryId { get; set; }
        public string? Value { get; set; }
        public long? TagId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual Laboratory Laboratory { get; set; } = null!;
        public virtual Tag? Tag { get; set; }
    }
}
