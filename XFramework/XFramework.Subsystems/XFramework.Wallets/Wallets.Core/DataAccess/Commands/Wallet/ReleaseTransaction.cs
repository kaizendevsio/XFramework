using Microsoft.Extensions.Logging;
using Wallets.Domain.Generic.Contracts.Requests;
using XFramework.Core.Services;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.Enums;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Generic.Contracts;

public class ReleaseTransaction(
    DbContext dbContext,
    ILogger<ReleaseTransaction> logger,
    ITenantService tenantService
)
    : IRequestHandler<ReleaseTransactionRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(ReleaseTransactionRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        // Attempt to find the transaction by ReferenceNumber
        var transaction = await dbContext.Set<WalletTransaction>()
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.TenantId == tenant.Id, cancellationToken);

        if (transaction == null)
        {
            logger.LogInformation("Transaction with Id {Id} not found", request.Id);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Transaction not found." };
        }

        if (!transaction.Held)
        {
            logger.LogInformation("Transaction with Id {Id} is not on hold", request.Id);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Transaction is not on hold." };
        }

        // Update the transaction to reflect it's no longer held
        transaction.Held = false;
        transaction.Released = true;

        // Fetch the associated wallets to update their balances
        var wallet = await dbContext.Set<Wallet>().FirstOrDefaultAsync(i => i.Id == transaction.WalletId, cancellationToken);
        if (wallet != null)
        {
            // Assuming the amount was deducted from the balance when put on hold,
            // and now needs to be made available again.
            if (transaction.TransactionType is TransactionType.Credit)
            {
                wallet.Balance += transaction.Amount;
                wallet.TransferableBalance += transaction.Amount;
                wallet.CreditOnHoldBalance -= transaction.Amount;
            }
            else if (transaction.TransactionType is TransactionType.Debit)
            {
                wallet.Balance -= transaction.Amount;
                wallet.DebitOnHoldBalance -= transaction.Amount;
            }
        }

        // Here you would also handle the recipient wallet if necessary,
        // For example, crediting the amount to the recipient's available balance.

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Transaction with Id {Id} released successfully", request.Id);

            return new CmdResponse { HttpStatusCode = HttpStatusCode.OK };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "A concurrency conflict occurred while releasing transaction with Id {Id}", request.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while releasing transaction with Id {Id}", request.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}
