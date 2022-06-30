using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class BinaryList
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }
        public long? TargetUserMapId { get; set; }
        public long? SourceUserMapId { get; set; }
        public int? Placement { get; set; }
        public int? Level { get; set; }
        public string Guid { get; set; }

        public virtual BinaryMap SourceUserMap { get; set; }
        public virtual BinaryMap TargetUserMap { get; set; }
    }
}
