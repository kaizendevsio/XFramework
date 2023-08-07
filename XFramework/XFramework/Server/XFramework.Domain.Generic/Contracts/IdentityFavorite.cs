namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityFavorite
{
    public Guid Id { get; set; }

    public Guid? FavoriteTypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Data { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    
    public virtual RegistryFavoriteType? FavoriteType { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}
