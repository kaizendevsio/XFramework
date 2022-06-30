using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class RegistryConfigurationGroup
    {
        public RegistryConfigurationGroup()
        {
            RegistryConfigurations = new HashSet<RegistryConfiguration>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }
        public string Guid { get; set; }

        public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; }
    }
}
