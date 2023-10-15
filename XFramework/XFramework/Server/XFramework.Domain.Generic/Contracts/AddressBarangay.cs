namespace XFramework.Domain.Generic.Contracts;

public partial record AddressBarangay : BaseModel
{
    public long? Code { get; set; }

    public string? Description { get; set; }

    public long CityCodeId { get; set; }

    public int? RegCode { get; set; }

    public int? ProvCode { get; set; }


    public virtual AddressCity? CityCode { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}