using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryService
    {
        public LaboratoryService()
        {
            ConsultationJobOrderLaboratories = new HashSet<ConsultationJobOrderLaboratory>();
            LaboratoryJobOrderDetails = new HashSet<LaboratoryJobOrderDetail>();
            LaboratoryServiceTags = new HashSet<LaboratoryServiceTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public long LaboratoryLocationId { get; set; }
        public long LaboratoryId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? MaxDiscount { get; set; }
        public decimal? Quantity { get; set; }
        public long? UnitId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual LaboratoryServiceEntity Entity { get; set; } = null!;
        public virtual Laboratory Laboratory { get; set; } = null!;
        public virtual LaboratoryLocation LaboratoryLocation { get; set; } = null!;
        public virtual Unit? Unit { get; set; }
        public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; }
        public virtual ICollection<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; }
        public virtual ICollection<LaboratoryServiceTag> LaboratoryServiceTags { get; set; }
    }
}
