namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityIdentityType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; set; } = new List<CommunityIdentity>();
}