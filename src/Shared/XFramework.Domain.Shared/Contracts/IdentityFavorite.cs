﻿using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IdentityFavorite : BaseModel
{
    public Guid? FavoriteTypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Data { get; set; }


    public virtual RegistryFavoriteType? FavoriteType { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}