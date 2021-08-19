using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblGateway
    {
        public TblGateway()
        {
            TblUserDepositRequests = new HashSet<TblUserDepositRequest>();
        }

        public long Id { get; set; }
        public long GatewayCategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal ServiceCharge { get; set; }
        public string Image { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? GatewayEndpointId { get; set; }
        public decimal? Discount { get; set; }
        public decimal ConvenienceFee { get; set; }

        public virtual TblGatewayCategory GatewayCategory { get; set; }
        public virtual TblGatewayEndpoint GatewayEndpoint { get; set; }
        public virtual ICollection<TblUserDepositRequest> TblUserDepositRequests { get; set; }
    }
}
