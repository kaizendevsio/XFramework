using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects;

public partial class AddressBarangay
{
    public long Id { get; set; }

    public long? Code { get; set; }

    public string? Description { get; set; }

    public long? CityCode { get; set; }

    public int? RegCode { get; set; }

    public int? ProvCode { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Guid { get; set; } = null!;

    public virtual AddressCity? CityCodeNavigation { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}
