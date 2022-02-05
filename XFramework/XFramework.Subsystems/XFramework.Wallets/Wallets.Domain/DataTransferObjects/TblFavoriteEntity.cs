using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblFavoriteEntity
    {
        public TblFavoriteEntity()
        {
            TblUserFavorites = new HashSet<TblUserFavorite>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual ICollection<TblUserFavorite> TblUserFavorites { get; set; }
    }
}
