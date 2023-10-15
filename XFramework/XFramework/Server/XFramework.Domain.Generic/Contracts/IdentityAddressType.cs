namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityAddressType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();
}