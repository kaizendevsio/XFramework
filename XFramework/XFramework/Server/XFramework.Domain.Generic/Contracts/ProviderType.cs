namespace XFramework.Domain.Generic.Contracts;

public partial class ProviderType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<BillerCategory> BillerCategories { get; } = new List<BillerCategory>();

    public virtual ICollection<ProviderEndpoint> ProviderEndpoints { get; } = new List<ProviderEndpoint>();
}