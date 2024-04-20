using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class AddressBarangay : BaseModel
{
    public long? Code { get; set; }

    public string? Description { get; set; }

    public long CityCodeId { get; set; }

    public int? RegCode { get; set; }

    public int? ProvCode { get; set; }


    public virtual AddressCity? CityCode { get; set; }

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}