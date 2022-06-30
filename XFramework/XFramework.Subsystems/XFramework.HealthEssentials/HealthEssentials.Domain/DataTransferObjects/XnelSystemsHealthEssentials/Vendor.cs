using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Vendor
    {
        public Vendor()
        {
            MedicineVendors = new HashSet<MedicineVendor>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsGenericProvider { get; set; }
        public string Guid { get; set; } = null!;

        public virtual VendorEntity Entity { get; set; } = null!;
        public virtual ICollection<MedicineVendor> MedicineVendors { get; set; }
    }
}
