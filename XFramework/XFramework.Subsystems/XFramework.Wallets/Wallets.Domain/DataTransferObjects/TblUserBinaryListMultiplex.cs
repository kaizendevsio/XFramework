using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBinaryListMultiplex
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
        public long? BusinessPackageId { get; set; }
        public long? LeftCount { get; set; }
        public long? RightCount { get; set; }
        public long? Level { get; set; }
        public long? UserMapId { get; set; }

        public virtual TblUserMap UserMap { get; set; }
    }
}
