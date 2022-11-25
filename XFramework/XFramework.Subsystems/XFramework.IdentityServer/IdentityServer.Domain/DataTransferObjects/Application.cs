using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects;

public partial class Application
{
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

    public virtual ICollection<IdentityCredential> IdentityCredentials { get; } = new List<IdentityCredential>();

    public virtual ICollection<IdentityInformation> IdentityInformations { get; } = new List<IdentityInformation>();

    public virtual ICollection<IdentityRoleEntity> IdentityRoleEntities { get; } = new List<IdentityRoleEntity>();

    public virtual ICollection<Log> Logs { get; } = new List<Log>();

    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; } = new List<RegistryConfiguration>();
}
