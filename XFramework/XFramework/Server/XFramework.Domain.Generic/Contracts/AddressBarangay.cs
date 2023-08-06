namespace XFramework.Domain.Generic.Contracts;

public partial class AddressBarangay
{
    public Guid Id { get; set; }

    public long? Code { get; set; }

    public string? Description { get; set; }

    public long? CityCode { get; set; }

    public int? RegCode { get; set; }

    public int? ProvCode { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    
    public virtual AddressCity? CityCodeNavigation { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}
