using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserTickets
    {
        public TblUserTickets()
        {
            TblUserTicketTransfers = new HashSet<TblUserTicketTransfers>();
        }

        public long Id { get; set; }
        public string PhoneNumber { get; set; }
        public int? TicketNumber { get; set; }
        public long? TicketTypeId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }
        public bool? IsEnabled { get; set; }
        public string Uuid { get; set; }
        public long? PinId { get; set; }
        public int? DeductionType { get; set; }

        public virtual TblPins Pin { get; set; }
        public virtual TblTickets TicketType { get; set; }
        public virtual ICollection<TblUserTicketTransfers> TblUserTicketTransfers { get; set; }
    }
}
