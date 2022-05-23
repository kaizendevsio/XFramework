using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class StorageFileIdentifier
    {
        public StorageFileIdentifier()
        {
            StorageFiles = new HashSet<StorageFile>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Guid { get; set; }
        public long GroupId { get; set; }

        public virtual StorageFileIdentifierGroup Group { get; set; }
        public virtual ICollection<StorageFile> StorageFiles { get; set; }
    }
}
