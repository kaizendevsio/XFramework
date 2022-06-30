using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class StorageFileIdentifierGroup
    {
        public StorageFileIdentifierGroup()
        {
            StorageFileIdentifiers = new HashSet<StorageFileIdentifier>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }

        public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; set; }
    }
}
