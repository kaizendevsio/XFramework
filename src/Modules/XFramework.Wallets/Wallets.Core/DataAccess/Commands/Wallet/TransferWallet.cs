using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Shared.Contracts;

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
        
        // Check if the amount is within the transferable balance
        if (request.Amount > senderWallet.TransferableBalance)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Amount exceeds transferable balance"
            };
        }
        
        // Check if the amount is within the min transferable amount
        if (request.Amount < senderWallet.MinTransferRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {senderWallet.MinTransferRule}"
            };
        }
        
        // Check if the amount is within the max transferable amount
        if (request.Amount > senderWallet.MaxTransferRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.MaxTransferRule}"
            };
        }
        
        // Check if amount is within the bond balance rule
        if (senderWallet.BondBalanceRule.HasValue && request.Amount > senderWallet.BondBalanceRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.BondBalanceRule}"
            };
        }
        
        // Check if the amount is within the maintaining balance rule
        if (senderWallet.MaintainingBalanceRule.HasValue && senderWallet.Balance - totalDeduction < senderWallet.MaintainingBalanceRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.MaintainingBalanceRule}"
            };
        }
        
        // Check if the recipient wallet is within the min transferable amount
        if (request.Amount < recipientWallet.MinTransferRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {recipientWallet.MinTransferRule}"
            };
        }
        
        // Check if the recipient wallet is within the max transferable amount
        if (request.Amount > recipientWallet.MaxTransferRule)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {recipientWallet.MaxTransferRule}"
            };
        }
        
        // Check if the recipient wallet is within the bond balance rule

        // Perform the transfer and deduct the fee

        var previousSenderBalance = senderWallet.Balance;
        var previousRecipientBalance = recipientWallet.Balance;
        
        var previousSenderDebitOnHoldBalance = senderWallet.DebitOnHoldBalance;
        var previousSenderCreditOnHoldBalance = senderWallet.CreditOnHoldBalance;
        
        var previousRecipientDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance;
        var previousRecipientCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance;
        
        if (request.OnHold)
        {
            senderWallet.DebitOnHoldBalance += request.Amount;
            senderWallet.TransferableBalance -= request.Amount;
            
            recipientWallet.CreditOnHoldBalance += request.Amount;
        }
        else
        {
            senderWallet.Balance -= totalDeduction;
            senderWallet.TransferableBalance -= totalDeduction;
            
            recipientWallet.Balance += request.Amount;
            recipientWallet.TransferableBalance += request.Amount;
        }

        // Record the transactions
        var senderTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = senderWallet.Id,
            Amount = totalDeduction,
            PreviousBalance = previousSenderBalance,
            PreviousDebitOnHoldBalance = previousSenderDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = previousSenderCreditOnHoldBalance,
            RunningDebitOnHoldBalance = senderWallet.DebitOnHoldBalance,
            RunningCreditOnHoldBalance = senderWallet.CreditOnHoldBalance,
            RunningBalance = senderWallet.Balance,
            RunningTotalBalance = senderWallet.TotalBalance,
            RunningAvailableBalance = senderWallet.AvailableBalance,
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
            PreviousDebitOnHoldBalance = previousRecipientDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = previousRecipientCreditOnHoldBalance,
            RunningBalance = recipientWallet.Balance,
            RunningTotalBalance = recipientWallet.TotalBalance,
            RunningAvailableBalance = recipientWallet.AvailableBalance,
            RunningCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance,
            RunningDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance,
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