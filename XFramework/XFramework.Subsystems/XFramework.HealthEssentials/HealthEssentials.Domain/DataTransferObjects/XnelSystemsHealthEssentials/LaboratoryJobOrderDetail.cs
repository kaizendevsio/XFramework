using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryJobOrderDetail
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LaboratoryJobOrderId { get; set; }
        public long LaboratoryServiceId { get; set; }
        public string? Quantity { get; set; }
        public string? Remarks { get; set; }
        public short? Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Guid { get; set; } = null!;

        public virtual LaboratoryJobOrder LaboratoryJobOrder { get; set; } = null!;
        public virtual LaboratoryService LaboratoryService { get; set; } = null!;
    }
}
