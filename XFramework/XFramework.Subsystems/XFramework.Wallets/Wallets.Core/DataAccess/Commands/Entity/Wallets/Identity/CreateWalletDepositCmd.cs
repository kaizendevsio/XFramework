using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

public class CreateWalletDepositCmd : CreateWalletDepositRequest, IRequest<CmdResponse<DepositResponse>>
{
    
}