using Microsoft.Extensions.Logging;
using Wallets.Domain.Generic.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Contexts;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using XFramework.Domain.Generic.Contracts;

namespace Wallets.Core.DataAccess.Commands.Wallet;

public class TransferWallet(
    AppDbContext appDbContext,
    ILogger<TransferWallet> logger,
    ITenantService tenantService
)
    : IRequestHandler<TransferWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(TransferWalletRequest request, CancellationToken cancellationToken)
    {
        // Validate the amount and fee
        if (request.Amount <= 0 || request.Fee < 0)
        {
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid amount or fee" };
        }

        // Fetch the sender and recipient wallet
        var senderWallet = await appDbContext.Wallets.FindAsync(request.CredentialId, request.WalletTypeId);
        var recipientWallet = await appDbContext.Wallets.FindAsync(request.RecipientCredentialId, request.WalletTypeId);

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
        senderWallet.Balance -= totalDeduction;
        recipientWallet.Balance += request.Amount;

        // Record the transactions
        var senderTransaction = new WalletTransaction
        {
            CredentialId = request.CredentialId,
            WalletId = senderWallet.Id,
            Amount = -totalDeduction,
            PreviousBalance = (senderWallet.Balance ?? 0) + totalDeduction,
            RunningBalance = senderWallet.Balance,
            Remarks = request.Remarks,
            Description = $"Transfer to {request.RecipientCredentialId}"
        };

        var recipientTransaction = new WalletTransaction
        {
            CredentialId = request.RecipientCredentialId,
            WalletId = recipientWallet.Id,
            Amount = request.Amount,
            PreviousBalance = (recipientWallet.Balance ?? 0) - request.Amount,
            RunningBalance = recipientWallet.Balance,
            Remarks = request.Remarks,
            Description = $"Received from {request.CredentialId}"
        };

        appDbContext.WalletTransactions.Add(senderTransaction);
        appDbContext.WalletTransactions.Add(recipientTransaction);

        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.Accepted, Message = "Transfer successful" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transferring wallet balance");
            return new CmdResponse
                { HttpStatusCode = HttpStatusCode.InternalServerError, Message = "Error processing request" };
        }
    }
}