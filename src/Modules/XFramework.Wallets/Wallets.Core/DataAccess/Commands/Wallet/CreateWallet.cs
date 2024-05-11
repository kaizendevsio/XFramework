using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Shared.Contracts;

public class CreateWallet(
    DbContext appDbContext,
    ILogger<CreateWallet> logger,
    ITenantService tenantService,
    IHelperService helperService,
    IRequestHandler<Create<Wallet>, CmdResponse<Wallet>> baseHandler
)
    : ICreateHandler<Wallet>, IDecorator
{
    public async Task<CmdResponse<Wallet>> Handle(Create<Wallet> request, CancellationToken cancellationToken)
    {
        var walletType = await appDbContext.Set<WalletType>()
            .Where(x => x.Id == request.Model.WalletTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (walletType is null)
        {
            logger.LogWarning("Error creating wallet. Wallet type not found, wallet type ID {WalletTypeId} credential ID {CredentialId}",
                request.Model.WalletTypeId, request.Model.CredentialId);
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "Wallet type not found"
            };
        }
        
        // Set the wallet type
        request.Model.WalletTypeId = walletType.Id;
        request.Model.BondBalanceRule = walletType.BondBalanceRule;
        request.Model.MaintainingBalanceRule = walletType.MaintainingBalanceRule;
        request.Model.MinTransferRule = walletType.MinTransferRule;
        request.Model.MaxTransferRule = walletType.MaxTransferRule;
        request.Model.AccountNumber = $"{helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
        
        // check if the account number is unique
        
        checkAccountNumber:
        var accountNumberExists = await appDbContext.Set<Wallet>()
            .AnyAsync(x => x.AccountNumber == request.Model.AccountNumber, cancellationToken);

        if (accountNumberExists)
        {
            request.Model.AccountNumber = $"{helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
            goto checkAccountNumber;
        }
        
        return await baseHandler.Handle(request, cancellationToken);
    }
}