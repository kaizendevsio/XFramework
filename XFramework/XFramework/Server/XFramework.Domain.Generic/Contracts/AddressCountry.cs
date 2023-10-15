namespace XFramework.Domain.Generic.Contracts;

public partial record AddressCountry : BaseModel
{
    public string? IsoCode2 { get; set; }

    public string? IsoCode3 { get; set; }

    public string? Name { get; set; }

    public string? Language { get; set; }

    public string? PhoneCountryCode { get; set; }

    public Guid CurrencyId { get; set; }


    public virtual ICollection<AddressRegion> AddressRegions { get; } = new List<AddressRegion>();

    public virtual CurrencyType? Currency { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}