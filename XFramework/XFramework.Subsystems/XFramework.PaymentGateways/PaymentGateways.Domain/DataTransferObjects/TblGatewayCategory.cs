using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblGatewayCategory
    {
        public TblGatewayCategory()
        {
            TblGateways = new HashSet<TblGateway>();
        }

        public long Id { get; set; }
        public long GatewayEntityId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public virtual TblGatewayEntity GatewayEntity { get; set; }
        public virtual ICollection<TblGateway> TblGateways { get; set; }
    }
}
