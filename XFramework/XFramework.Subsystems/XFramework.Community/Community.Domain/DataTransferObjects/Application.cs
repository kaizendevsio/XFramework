using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class Application
    {
        public Application()
        {
            IdentityCredentials = new HashSet<IdentityCredential>();
            IdentityRoleEntities = new HashSet<IdentityRoleEntity>();
            Logs = new HashSet<Log>();
            RegistryConfigurations = new HashSet<RegistryConfiguration>();
        }

        public long Id { get; set; }
        public string? AppName { get; set; }
        public string? Description { get; set; }
        public string Guid { get; set; } = null!;
        public short? Status { get; set; }
        public DateTime? Expiration { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public long? ParentAppId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long EnterpriseId { get; set; }
        public decimal Version { get; set; }

        public virtual Enterprise Enterprise { get; set; } = null!;
        public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; }
        public virtual ICollection<IdentityRoleEntity> IdentityRoleEntities { get; set; }
        public virtual ICollection<Log> Logs { get; set; }
        public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; }
    }
}
