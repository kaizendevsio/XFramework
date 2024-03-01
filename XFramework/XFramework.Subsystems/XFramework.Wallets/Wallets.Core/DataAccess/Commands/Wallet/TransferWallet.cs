using Microsoft.Extensions.Logging;
using Wallets.Domain.Generic.Contracts.Requests;
using XFramework.Core.Services;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.Enums;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Generic.Contracts;

public class TransferWallet(
    DbContext dbContext,
    ILogger<TransferWallet> logger,
    ITenantService tenantService
)
    : IRequestHandler<TransferWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(TransferWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        // Validate the amount and fee
        if (request.Amount <= 0 || request.Fee < 0)
        {
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid amount or fee" };
        }

        // Fetch the sender and recipient wallet
        
        IQueryable<Wallet> query = dbContext.Set<Wallet>();

        var senderWallet = await query
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.CredentialId)
            .Where(x => x.WalletTypeId == request.WalletTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        var recipientWallet = await query
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.RecipientCredentialId)
            .Where(x => x.WalletTypeId == request.WalletTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (senderWallet == null || recipientWallet == null)
        {
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
        }

        // Check for self-transfer
        if (request.CredentialId == request.RecipientCredentialId)
        {
            return new CmdResponse
                { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Cannot transfer to the same wallet" };
        }

        // Check if sender has enough balance
        var totalDeduction = request.Amount + request.Fee;
        if (senderWallet.Balance < totalDeduction)
        {
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Insufficient balance" };
        }

        // Perform the transfer and deduct the fee

        var previousSenderBalance = senderWallet.Balance ?? 0;
        var previousRecipientBalance = recipientWallet.Balance ?? 0;
        
        var previousSenderOnHoldBalance = senderWallet.OnHoldBalance ?? 0;
        var previousRecipientOnHoldBalance = recipientWallet.OnHoldBalance ?? 0;
        
        if (request.OnHold is true)
        {
            senderWallet.OnHoldBalance -= request.Amount;
            recipientWallet.OnHoldBalance += request.Amount;
        }
        else
        {
            senderWallet.Balance -= totalDeduction;
            recipientWallet.Balance += request.Amount;
        }

        // Record the transactions
        var senderTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = senderWallet.Id,
            Amount = totalDeduction,
            PreviousBalance = previousSenderBalance,
            PreviousOnHoldBalance = previousSenderOnHoldBalance,
            RunningOnHoldBalance = senderWallet.OnHoldBalance,
            RunningBalance = senderWallet.Balance,
            Remarks = request.Remarks,
            Description = $"Transfer to {request.RecipientCredentialId}",
            TransactionType = TransactionType.Debit,
            Held = request.OnHold,
            Released = false,
            ReferenceNumber = request.ReferenceNumber
        };

        var recipientTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.RecipientCredentialId,
            WalletId = recipientWallet.Id,
            Amount = request.Amount,
            PreviousBalance = previousRecipientBalance,
            PreviousOnHoldBalance = previousRecipientOnHoldBalance,
            RunningBalance = recipientWallet.Balance,
            RunningOnHoldBalance = recipientWallet.OnHoldBalance,
            Remarks = request.Remarks,
            Description = $"Received from {request.CredentialId}",
            TransactionType = TransactionType.Credit,
            Held = request.OnHold,
            Released = false,
            ReferenceNumber = request.ReferenceNumber
        };

        dbContext.Set<WalletTransaction>().Add(senderTransaction);
        dbContext.Set<WalletTransaction>().Add(recipientTransaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Wallet transfer from {SenderCredentialId} to {RecipientCredentialId} was successful", request.CredentialId, request.RecipientCredentialId);

            return new CmdResponse { HttpStatusCode = HttpStatusCode.OK };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}