using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class AddressBarangay
{
    public AddressBarangay()
    {
        IdentityAddresses = new HashSet<IdentityAddress>();
    }

    public long Id { get; set; }
    public long? Code { get; set; }
    public string Description { get; set; }
    public long? CityCode { get; set; }
    public int? RegCode { get; set; }
    public int? ProvCode { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Guid { get; set; }

    public virtual AddressCity CityCodeNavigation { get; set; }
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; }
}