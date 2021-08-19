using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblGatewayEndpoint
    {
        public TblGatewayEndpoint()
        {
            TblGateways = new HashSet<TblGateway>();
        }

        public long Id { get; set; }
        public long? GatewayEntityId { get; set; }
        public string Name { get; set; }
        public string BaseUrlEndpoint { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }
        public string UrlEndpoint { get; set; }

        public virtual TblGatewayEntity GatewayEntity { get; set; }
        public virtual ICollection<TblGateway> TblGateways { get; set; }
    }
}
