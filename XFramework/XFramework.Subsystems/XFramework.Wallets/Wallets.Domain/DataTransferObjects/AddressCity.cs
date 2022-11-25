using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects;

public partial class AddressCity
{
    public long Id { get; set; }

    public long PsgcCode { get; set; }

    public string? Description { get; set; }

    public long ProvCode { get; set; }

    public long Code { get; set; }

    public int? RegCode { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<AddressBarangay> AddressBarangays { get; } = new List<AddressBarangay>();

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();

    public virtual AddressProvince ProvCodeNavigation { get; set; } = null!;
}
