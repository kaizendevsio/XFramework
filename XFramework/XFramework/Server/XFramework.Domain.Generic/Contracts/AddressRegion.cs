namespace XFramework.Domain.Generic.Contracts;

public partial class AddressRegion : BaseModel
{
    public int PsgcCode { get; set; }

    public string? Description { get; set; }

    public long Code { get; set; }


    public Guid CountryId { get; set; }

    public virtual ICollection<AddressProvince> AddressProvinces { get; set; } = new List<AddressProvince>();

    public virtual AddressCountry? Country { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}