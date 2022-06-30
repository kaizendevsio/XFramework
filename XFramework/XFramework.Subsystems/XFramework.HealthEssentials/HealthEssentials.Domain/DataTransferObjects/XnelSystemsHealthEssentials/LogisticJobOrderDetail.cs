using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LogisticJobOrderDetail
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LogisticJobOrderId { get; set; }
        public short Status { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public long Quantity { get; set; }
        public long? UnitId { get; set; }
        public string Guid { get; set; } = null!;
        public long UnitPrice { get; set; }
        public long Discount { get; set; }
        public long DiscountType { get; set; }
        public string LocationGuid { get; set; } = null!;

        public virtual LogisticJobOrder LogisticJobOrder { get; set; } = null!;
        public virtual Unit? Unit { get; set; }
    }
}
