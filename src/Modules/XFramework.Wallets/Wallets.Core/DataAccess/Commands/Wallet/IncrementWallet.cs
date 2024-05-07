using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Shared.Contracts;

public class IncrementWallet(
    DbContext dbContext,
    ILogger<ReleaseTransaction> logger,
    ITenantService tenantService
)
    : IRequestHandler<IncrementWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(IncrementWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        if (request.Amount <= 0)
        {
            logger.LogWarning("Invalid increment amount for wallet ID {WalletId}", request.WalletId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid increment amount" };
        }

        var wallet = await dbContext.Set<Wallet>()
            .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
            .FirstOrDefaultAsync(cancellationToken);

        if (wallet == null)
        {
            logger.LogWarning("Wallet not found for ID {WalletId}", request.WalletId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
        }

        // Check if the amount is within the min transferable amount
        if (wallet.MinTransferRule.HasValue && request.Amount < wallet.MinTransferRule.Value)
        {
            logger.LogWarning("Amount is below the minimum transferable amount for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {wallet.MinTransferRule.Value}"
            };
        }

        // Check if the amount is within the max transferable amount
        if (wallet.MaxTransferRule.HasValue && request.Amount > wallet.MaxTransferRule.Value)
        {
            logger.LogWarning("Amount exceeds the maximum transferable amount for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {wallet.MaxTransferRule.Value}"
            };
        }

        // Increment the wallet balance
        var previousBalance = wallet.Balance;
        var previousCreditOnHoldBalance = wallet.CreditOnHoldBalance;
        
        if (request.OnHold)
        {
            wallet.CreditOnHoldBalance += request.Amount;
        }
        else
        {
            wallet.Balance += request.Amount;
        }

        // Ensure maintaining balance is upheld
        if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
        {
            logger.LogWarning("Incrementing would violate maintaining balance rule for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Balance after increment must not drop below {wallet.MaintainingBalanceRule.Value}"
            };
        }

        // Record the transaction
        var transaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = wallet.Id,
            Amount = request.Amount,
            TransactionFee = request.Fee,
            ConvenienceFee = request.ConvenienceFee,
            PreviousBalance = previousBalance,
            RunningBalance = wallet.Balance,
            PreviousCreditOnHoldBalance = previousCreditOnHoldBalance,
            RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance,
            Remarks = request.Remarks,
            TransactionType = TransactionType.Credit,
            Held = request.OnHold,
            Released = !request.OnHold,
            ReferenceNumber = request.ReferenceNumber
        };

        dbContext.Set<WalletTransaction>().Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Wallet balance incremented successfully for Wallet ID {WalletId}", wallet.Id);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Wallet balance incremented successfully"
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while incrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while incrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}