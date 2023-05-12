using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class GatewayResponse
    {
        public long Id { get; set; }
        public string Guid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string Description { get; set; }
        public long ResponseStatusTypeId { get; set; }
        public long GatewayResponseTypeId { get; set; }

        public virtual GatewayResponseType GatewayResponseType { get; set; }
        public virtual GatewayResponseStatusType ResponseStatusType { get; set; }
    }
}
