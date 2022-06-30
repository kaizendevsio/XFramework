using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Ailment
    {
        public Ailment()
        {
            AilmentTags = new HashSet<AilmentTag>();
            PatientAilments = new HashSet<PatientAilment>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? OtherName { get; set; }
        public string? Description { get; set; }
        public string Guid { get; set; } = null!;

        public virtual AilmentEntity Entity { get; set; } = null!;
        public virtual ICollection<AilmentTag> AilmentTags { get; set; }
        public virtual ICollection<PatientAilment> PatientAilments { get; set; }
    }
}
