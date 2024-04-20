using System.Collections.Generic;
using PaymentGateways.Domain.BusinessObjects.EcLink;
using PaymentGateways.Domain.BusinessObjects.Paynamics;
using PaymentGateways.Domain.DataTransferObjects;
using XFramework.Domain.Generic.Interfaces;

namespace PaymentGateways.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public EcPayConfiguration EcPayConfiguration { get; set; }
    public PaynamicsConfiguration PaynamicsConfiguration { get; set; }
    public List<GatewayCategory> GatewayCategoryList { get; set; }
    public List<DepositRequest> UserDepositRequestList { get; set; }
    public List<GatewayResponse> GatewayResponseList { get; set; }
}