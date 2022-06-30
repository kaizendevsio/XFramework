using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class BinaryListMultiplex
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
        public long? BusinessPackageId { get; set; }
        public long? LeftCount { get; set; }
        public long? RightCount { get; set; }
        public long? Level { get; set; }
        public long? BinaryMapId { get; set; }
        public string Guid { get; set; }

        public virtual BinaryMap BinaryMap { get; set; }
    }
}
