using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryMember
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string CredentialGuid { get; set; } = null!;
        public long LaboratoryId { get; set; }
        public string? Value { get; set; }
        public string Guid { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Status { get; set; }
        public string? Description { get; set; }
        public long? LaboratoryLocationId { get; set; }

        public virtual Laboratory Laboratory { get; set; } = null!;
        public virtual LaboratoryLocation? LaboratoryLocation { get; set; }
    }
}
