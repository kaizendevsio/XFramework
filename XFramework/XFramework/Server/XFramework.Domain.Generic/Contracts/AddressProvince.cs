namespace XFramework.Domain.Generic.Contracts;

public partial class AddressProvince : BaseModel
{
    public long PsgcCode { get; set; }

    public string? Description { get; set; }

    public long RegCodeId { get; set; }

    public long Code { get; set; }


    public virtual ICollection<AddressCity> AddressCities { get; set; } = new List<AddressCity>();

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    public virtual AddressRegion RegCode { get; set; } = null!;
}