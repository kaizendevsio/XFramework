using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class StorageFile
    {
        public StorageFile()
        {
            MessageFiles = new HashSet<MessageFile>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string ContentPath { get; set; } = null!;
        public long EntityId { get; set; }
        public string Guid { get; set; } = null!;
        public string? IdentifierGuid { get; set; }
        public decimal? FileSize { get; set; }
        public DateTime? ExpireAt { get; set; }
        public long? StorageFileIdentifierId { get; set; }
        public string? Hash { get; set; }

        public virtual StorageFileEntity Entity { get; set; } = null!;
        public virtual StorageFileIdentifier? StorageFileIdentifier { get; set; }
        public virtual ICollection<MessageFile> MessageFiles { get; set; }
    }
}
