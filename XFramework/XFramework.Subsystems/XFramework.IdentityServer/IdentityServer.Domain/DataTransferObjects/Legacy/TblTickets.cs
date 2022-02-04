using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblTickets
{
    public TblTickets()
    {
        TblUserTickets = new HashSet<TblUserTickets>();
    }

    public long Id { get; set; }
    public string Uuid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int? Type { get; set; }
    public decimal? Value { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDeleted { get; set; }

    public virtual ICollection<TblUserTickets> TblUserTickets { get; set; }
}