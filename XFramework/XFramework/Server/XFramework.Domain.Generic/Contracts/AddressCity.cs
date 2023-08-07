namespace XFramework.Domain.Generic.Contracts;

public partial class AddressCity
{
    public Guid Id { get; set; }

    public long PsgcCode { get; set; }

    public string? Description { get; set; }

    public long ProvCodeId { get; set; }

    public long Code { get; set; }

    public int? RegCode { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    
    public virtual ICollection<AddressBarangay> AddressBarangays { get; } = new List<AddressBarangay>();

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();

    public virtual AddressProvince ProvCode { get; set; } = null!;
}
