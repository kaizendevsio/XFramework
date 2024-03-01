namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityAddressType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}