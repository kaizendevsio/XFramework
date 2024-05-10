namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityFavorite : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? FavoriteTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? Data { get; set; }


    [MemoryPackOrder(3)]
    public virtual RegistryFavoriteType? FavoriteType { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}
