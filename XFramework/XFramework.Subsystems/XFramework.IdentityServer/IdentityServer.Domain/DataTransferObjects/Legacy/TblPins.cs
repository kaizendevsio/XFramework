using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblPins
    {
        public TblPins()
        {
            TblUserTickets = new HashSet<TblUserTickets>();
        }

        public long Id { get; set; }
        public string Value { get; set; }
        public int? Type { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }

        public virtual ICollection<TblUserTickets> TblUserTickets { get; set; }
    }
}
