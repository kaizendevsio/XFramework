namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class RegistryFavoriteType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; } = new List<IdentityFavorite>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
