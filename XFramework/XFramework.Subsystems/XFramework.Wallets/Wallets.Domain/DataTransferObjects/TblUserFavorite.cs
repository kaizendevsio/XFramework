using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserFavorite
    {
        public long Id { get; set; }
        public long? FavoriteEntityId { get; set; }
        public long? CredentialId { get; set; }
        public string Data { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual TblIdentityCredential Credential { get; set; }
        public virtual TblFavoriteEntity FavoriteEntity { get; set; }
    }
}
