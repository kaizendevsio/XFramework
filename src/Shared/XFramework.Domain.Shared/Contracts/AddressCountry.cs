using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class AddressCountry : BaseModel
{
    public string? IsoCode2 { get; set; }

    public string? IsoCode3 { get; set; }

    public string? Name { get; set; }

    public string? Language { get; set; }

    public string? PhoneCountryCode { get; set; }

    public Guid CurrencyId { get; set; }


    public virtual ICollection<AddressRegion> AddressRegions { get; set; } = new List<AddressRegion>();

    public virtual CurrencyType? Currency { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}