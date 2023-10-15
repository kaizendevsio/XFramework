namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityIdentityType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; } = new List<CommunityIdentity>();
}