using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class StorageFile
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string ContentPath { get; set; }
        public long EntityId { get; set; }
        public string Guid { get; set; }
        public string IdentifierGuid { get; set; }
        public decimal? FileSize { get; set; }
        public DateTime? ExpireAt { get; set; }
        public long? StorageFileIdentifierId { get; set; }
        public string Hash { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }

        public virtual StorageFileEntity Entity { get; set; }
        public virtual StorageFileIdentifier StorageFileIdentifier { get; set; }
    }
}
