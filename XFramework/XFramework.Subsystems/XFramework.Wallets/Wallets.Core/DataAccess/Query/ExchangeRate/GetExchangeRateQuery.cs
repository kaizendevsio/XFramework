using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.ExchangeRate;

public class GetExchangeRateQuery : GetExchangeRateRequest, IRequest<QueryResponse<ExchangeRateResponse>>
{
    
}