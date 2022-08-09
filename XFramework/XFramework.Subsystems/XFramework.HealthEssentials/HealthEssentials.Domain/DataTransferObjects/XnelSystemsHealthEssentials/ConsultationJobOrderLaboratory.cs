using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class ConsultationJobOrderLaboratory
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long ConsultationJobOrderId { get; set; }
        public long LaboratoryServiceId { get; set; }
        public long? SuggestedLaboratoryLocationId { get; set; }
        public string? Quantity { get; set; }
        public string? PrescriptionNote { get; set; }
        public string? Remarks { get; set; }
        public short? Status { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;
        public virtual LaboratoryServiceEntity LaboratoryService { get; set; } = null!;
        public virtual LaboratoryLocation? SuggestedLaboratoryLocation { get; set; }
    }
}
