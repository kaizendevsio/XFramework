using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblFavoriteEntity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
