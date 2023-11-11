namespace XFramework.Domain.Generic.Contracts;

public partial class RegistryFavoriteType : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; } = new List<IdentityFavorite>();
}