using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class StorageFile
    {
        public StorageFile()
        {
            CommunityContentFiles = new HashSet<CommunityContentFile>();
            CommunityIdentityFiles = new HashSet<CommunityIdentityFile>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string ContentPath { get; set; } = null!;
        public long EntityId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual StorageFileEntity Entity { get; set; } = null!;
        public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; set; }
        public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; }
    }
}
