using Wallets.Core.DataAccess.Commands.Handlers;

namespace Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

public class CreateExchangeRateCmd : CreateExchangeRateRequest, IRequest<CmdResponse<CreateExchangeRateCmd>>
{
    
}