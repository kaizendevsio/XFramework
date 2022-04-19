using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class IdentityFavorite
    {
        public long Id { get; set; }
        public long? FavoriteEntityId { get; set; }
        public long? IdentityCredentialId { get; set; }
        public string? Data { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public string Guid { get; set; } = null!;

        public virtual RegistryFavoriteEntity? FavoriteEntity { get; set; }
        public virtual IdentityCredential? IdentityCredential { get; set; }
    }
}
