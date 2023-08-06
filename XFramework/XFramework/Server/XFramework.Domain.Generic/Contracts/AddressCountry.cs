namespace XFramework.Domain.Generic.Contracts;

public partial class AddressCountry
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedOn { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? LastChanged { get; set; }

    public string? IsoCode2 { get; set; }

    public string? IsoCode3 { get; set; }

    public string? Name { get; set; }

    public string? Language { get; set; }

    public string? PhoneCountryCode { get; set; }

    public long? CurrencyId { get; set; }

    
    public virtual ICollection<AddressRegion> AddressRegions { get; } = new List<AddressRegion>();

    public virtual CurrencyType? Currency { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}
