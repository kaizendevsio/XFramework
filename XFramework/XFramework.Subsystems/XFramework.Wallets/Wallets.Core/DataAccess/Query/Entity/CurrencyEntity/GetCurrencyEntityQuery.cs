using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.Entity.CurrencyEntity;

public class GetCurrencyEntityQuery : GetCurrencyEntityRequest, IRequest<QueryResponse<CurrencyEntityResponse>>
{
    
}