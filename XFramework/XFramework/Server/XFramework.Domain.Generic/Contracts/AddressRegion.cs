﻿namespace XFramework.Domain.Generic.Contracts;

public partial record AddressRegion : BaseModel
{
    public int PsgcCode { get; set; }

    public string? Description { get; set; }

    public long Code { get; set; }


    public Guid CountryId { get; set; }

    public virtual ICollection<AddressProvince> AddressProvinces { get; } = new List<AddressProvince>();

    public virtual AddressCountry? Country { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}