using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class RegistryFavoriteType : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; } = new List<IdentityFavorite>();
}