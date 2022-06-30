using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;

namespace Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

public class UpdateExchangeRateCmd : UpdateExchangeRateRequest, IRequest<CmdResponse<UpdateExchangeRateCmd>>
{
    
}