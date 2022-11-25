using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects;

public partial class RegistryConfigurationGroup
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; } = new List<RegistryConfiguration>();
}
