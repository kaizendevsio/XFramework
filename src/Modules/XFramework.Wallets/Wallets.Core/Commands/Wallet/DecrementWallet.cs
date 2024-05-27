using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.Commands.Wallet;

public class DecrementWallet(
    DbContext dbContext,
    ILogger<ReleaseTransaction> logger,
    ITenantService tenantService
)
    : IRequestHandler<DecrementWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(DecrementWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
        if (request.Amount <= 0)
        {
            logger.LogWarning("Invalid increment amount for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}", request.WalletId, request.WalletTypeId, request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid increment amount" };
        }

        var wallet = request.WalletTypeId != Guid.Empty
            ? await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
                .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                .FirstOrDefaultAsync(cancellationToken)
            : await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
                .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
                .FirstOrDefaultAsync(cancellationToken);

        if (wallet == null)
        {
            logger.LogWarning("Wallet not found for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}", request.WalletId, request.WalletTypeId, request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
        }

        if (wallet.AvailableBalance < request.Amount)
        {
            logger.LogWarning("Insufficient funds for decrement for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Insufficient funds" };
        }

        var previousBalance = wallet.Balance;
        var previousDebitOnHoldBalance = wallet.DebitOnHoldBalance;
        
        if (request.OnHold)
        {
            wallet.DebitOnHoldBalance += request.Amount;
        }
        else
        {
            wallet.Balance -= request.Amount;
        }

        if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
        {
            logger.LogWarning("Decrementing would violate maintaining balance rule for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Balance after decrement must not drop below {wallet.MaintainingBalanceRule.Value}"
            };
        }

        var transaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = wallet.Id,
            Amount = -request.Amount,  // Negative because it's a decrement
            TransactionFee = request.Fee,
            ConvenienceFee = request.ConvenienceFee,
            PreviousBalance = previousBalance,
            RunningBalance = wallet.Balance,
            PreviousDebitOnHoldBalance = previousDebitOnHoldBalance,
            RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance,
            Remarks = request.Remarks,
            TransactionType = TransactionType.Debit,
            Held = request.OnHold,
            Released = !request.OnHold,
            ReferenceNumber = request.ReferenceNumber
        };

        dbContext.Set<WalletTransaction>().Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Wallet balance decremented successfully for Wallet ID {WalletId}", wallet.Id);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Wallet balance decremented successfully"
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while decrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while decrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}