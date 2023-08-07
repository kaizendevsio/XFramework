namespace XFramework.Domain.Generic.Contracts;

public partial class AddressProvince
{
    public Guid Id { get; set; }

    public long PsgcCode { get; set; }

    public string? Description { get; set; }

    public long RegCodeId { get; set; }

    public long Code { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    
    public virtual ICollection<AddressCity> AddressCities { get; } = new List<AddressCity>();

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();

    public virtual AddressRegion RegCode { get; set; } = null!;
}
