using Microsoft.Extensions.Logging;
using Wallets.Domain.Generic.Contracts.Requests;
using XFramework.Core.Services;
using Microsoft.EntityFrameworkCore;

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
            .FirstOrDefaultAsync(t => t.ReferenceNumber == request.ReferenceNumber && t.TenantId == tenant.Id, cancellationToken);

        if (transaction == null)
        {
            logger.LogInformation("Transaction with ReferenceNumber {ReferenceNumber} not found.", request.ReferenceNumber);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Transaction not found." };
        }

        if (!transaction.Held)
        {
            logger.LogInformation("Transaction with ReferenceNumber {ReferenceNumber} is not on hold.", request.ReferenceNumber);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Transaction is not on hold." };
        }

        // Update the transaction to reflect it's no longer held
        transaction.Held = false;
        transaction.Released = true;

        // Fetch the associated wallets to update their balances
        var senderWallet = await dbContext.Set<Wallet>().firfir(transaction.WalletId, cancellationToken);
        if (senderWallet != null)
        {
            // Assuming the amount was deducted from the balance when put on hold,
            // and now needs to be made available again.
            senderWallet.OnHoldBalance -= transaction.Amount;
        }

        // Here you would also handle the recipient wallet if necessary,
        // For example, crediting the amount to the recipient's available balance.

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Successfully released transaction with ReferenceNumber {ReferenceNumber}.", request.ReferenceNumber);

            return new CmdResponse { HttpStatusCode = HttpStatusCode.OK, Message = "Transaction released successfully." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error releasing transaction with ReferenceNumber {ReferenceNumber}.", request.ReferenceNumber);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.InternalServerError, Message = "Error releasing transaction." };
        }
    }
}
