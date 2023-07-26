﻿using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class Application
{
    public Application()
    {
        IdentityCredentials = new HashSet<IdentityCredential>();
        IdentityInformations = new HashSet<IdentityInformation>();
        IdentityRoleEntities = new HashSet<IdentityRoleEntity>();
        Logs = new HashSet<Log>();
        RegistryConfigurations = new HashSet<RegistryConfiguration>();
        WalletEntities = new HashSet<WalletEntity>();
    }

    public long Id { get; set; }
    public string AppName { get; set; }
    public string Description { get; set; }
    public string Guid { get; set; }
    public short? Status { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? AvailabilityDate { get; set; }
    public long? ParentAppId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long EnterpriseId { get; set; }
    public decimal Version { get; set; }

    public virtual Enterprise Enterprise { get; set; }
    public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; }
    public virtual ICollection<IdentityInformation> IdentityInformations { get; set; }
    public virtual ICollection<IdentityRoleEntity> IdentityRoleEntities { get; set; }
    public virtual ICollection<Log> Logs { get; set; }
    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; }
    public virtual ICollection<WalletEntity> WalletEntities { get; set; }
}