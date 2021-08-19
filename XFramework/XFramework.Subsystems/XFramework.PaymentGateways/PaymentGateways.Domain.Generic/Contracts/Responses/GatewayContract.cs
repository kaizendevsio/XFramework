using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses
{
    public class GatewayContract
    {
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
        public long? ProviderEndpointId { get; set; }
        public decimal? Discount { get; set; }
        public decimal ConvenienceFee { get; set; }
       
    }
}