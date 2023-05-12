using PaymentGateways.Core.Interfaces;
using System.Collections.Generic;
using PaymentGateways.Domain.BusinessObjects.EcLink;
using PaymentGateways.Domain.BusinessObjects.Paynamics;
using PaymentGateways.Domain.DataTransferObjects;

namespace PaymentGateways.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
        }

        public List<IdentitySessionBO> IdentitySessions { get; set; }
        public EcPayConfiguration EcPayConfiguration { get; set; }
        public PaynamicsConfiguration PaynamicsConfiguration { get; set; }
        public List<GatewayCategory> GatewayCategoryList { get; set; }
        public List<DepositRequest> UserDepositRequestList { get; set; }
        public List<GatewayResponse> GatewayResponseList { get; set; }
    }
}