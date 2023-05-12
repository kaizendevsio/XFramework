using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using Wallets.Domain.Generic.Contracts.Requests.Create;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace PaymentGateways.Integration.Interfaces;

public interface IPaymentGatewayWrapper : IXFrameworkService
{
    public Task<CmdResponse> CreateWithdrawalTransaction(CreateWithdrawalRequest request);
    public Task<QueryResponse<DepositResponse>> CreateDepositTransaction(CreateDepositRequest request);
}