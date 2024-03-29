﻿namespace XFramework.Domain.Generic.Contracts;

public partial class AddressCity : BaseModel
{
    public long PsgcCode { get; set; }

    public string? Description { get; set; }

    public long ProvCodeId { get; set; }

    public long Code { get; set; }

    public int? RegCode { get; set; }


    public virtual ICollection<AddressBarangay> AddressBarangays { get; set; } = new List<AddressBarangay>();

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    public virtual AddressProvince ProvCode { get; set; } = null!;
}