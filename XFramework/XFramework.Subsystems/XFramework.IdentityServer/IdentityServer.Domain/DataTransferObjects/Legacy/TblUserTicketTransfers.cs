using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserTicketTransfers
    {
        public long Id { get; set; }
        public string SourcePhoneNumber { get; set; }
        public string TargetPhoneNumber { get; set; }
        public long? UserTicketId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual TblUserTickets UserTicket { get; set; }
    }
}
