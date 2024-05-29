using System.Security.Cryptography.X509Certificates;
using System.Text;
using IdentityServer.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.Commands.Wallet;

public class TransferWallet(
    DbContext dbContext,
    ILogger<TransferWallet> logger,
    IIdentityServerServiceWrapper identityServerService,
    ITenantService tenantService,
    IMediator mediator
)
    : IRequestHandler<TransferWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(TransferWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        // Validate the amount and fee
        if (request.TotalAmount <= 0 || request.Fee < 0)
        {
            logger.LogWarning("Invalid amount or fee while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid amount or fee" };
        }
        
        // Check if wallet type ID is provided
        if (request.WalletTypeId == Guid.Empty)
        {
            logger.LogWarning("Wallet type ID is required while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Wallet type ID is required" };
        }

        // Fetch the sender and recipient wallet
        
        IQueryable<XFramework.Domain.Shared.Contracts.Wallet> query = dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>();

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

        var senderUser = identityServerService.IdentityCredential.Get(
            id: request.CredentialId,
            includeNavigations: true,
            includes: [
                $"{nameof(IdentityCredential.IdentityInfo)}"
            ],
            tenantId: tenant.Id);
        
        var recipientUser = identityServerService.IdentityCredential.Get(
            id: request.RecipientCredentialId,
            includeNavigations: true,
            includes:
            [
                $"{nameof(IdentityCredential.IdentityInfo)}"
            ],
            tenantId: tenant.Id);
        
        await Task.WhenAll(senderUser, recipientUser);

        if (senderUser.Result.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogWarning("Sender not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Sender not found" };
        }
        
        if (recipientUser.Result.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogWarning("Recipient not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Recipient not found" };
        }
        
        if (senderWallet == null)
        {
            logger.LogWarning("Sender wallet not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
        }
        
        // if wallet does not exist, create a new wallet if wallet type ID is provided
        if (recipientWallet is null)
        {
            var createWallet = await mediator.Send(new Create<XFramework.Domain.Shared.Contracts.Wallet>(new()
            {
                CredentialId = request.RecipientCredentialId,
                WalletTypeId = request.WalletTypeId,
                Balance = 0
            })
            {
                Metadata = request.Metadata
            });
            if (createWallet.IsSuccess is false)
            {
                logger.LogWarning("Recipient wallet not found and could not be created while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
                return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
            }
            
            recipientWallet = createWallet.Response;
        }

        // Check for self-transfer
        if (request.CredentialId == request.RecipientCredentialId)
        {
            logger.LogWarning("Cannot transfer to the same wallet while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Cannot transfer to the same wallet" };
        }

        decimal totalDecrement = 0;
        decimal totalIncrement = 0;
        TransferDeductionType transferDeductionType;
        
        switch (request.TransferDeductionType)
        {
            // Check Transfer Deduction Type
            case TransferDeductionType.Default:
                var transferDeductionTypeConfig = await dbContext.Set<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Transfer:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    logger.LogWarning("Transfer deduction type configuration not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Transfer deduction type configuration not found" };
                }
                
                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    logger.LogWarning("Invalid transfer deduction type configuration while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid transfer deduction type configuration" };
                }
                
                switch (transferDeductionTypeFromConfig)
                {
                    case TransferDeductionType.DeductFromSender:
                        totalDecrement = request.TotalAmount + request.TotalFee;
                        totalIncrement = request.TotalAmount;
                        break;
                    case TransferDeductionType.DeductFromRecipient:
                        totalDecrement = request.TotalAmount;
                        totalIncrement = request.TotalAmount - request.TotalFee;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                transferDeductionType = transferDeductionTypeFromConfig;
                break;
            case TransferDeductionType.DeductFromSender:
                totalDecrement = request.TotalAmount + request.TotalFee;
                totalIncrement = request.TotalAmount;
                transferDeductionType = TransferDeductionType.DeductFromSender;
                break;
            case TransferDeductionType.DeductFromRecipient:
                totalDecrement = request.TotalAmount;
                totalIncrement = request.TotalAmount - request.TotalFee;
                transferDeductionType = TransferDeductionType.DeductFromRecipient;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        // Check if sender has enough balance
        if (senderWallet.Balance < totalDecrement)
        {
            logger.LogWarning("Insufficient balance while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Insufficient balance" };
        }
        
        // Check if the amount is within the transferable balance
        if (request.TotalAmount > senderWallet.TransferableBalance)
        {
            logger.LogWarning("Amount exceeds transferable balance while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Amount exceeds transferable balance"
            };
        }
        
        // Check if the amount is within the min transferable amount
        if (request.TotalAmount < senderWallet.MinTransferRule)
        {
            logger.LogWarning("Amount is below the minimum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {senderWallet.MinTransferRule}"
            };
        }
        
        // Check if the amount is within the max transferable amount
        if (request.TotalAmount > senderWallet.MaxTransferRule)
        {
            logger.LogWarning("Amount exceeds the maximum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.MaxTransferRule}"
            };
        }
        
        // Check if amount is within the bond balance rule
        if (senderWallet.BondBalanceRule.HasValue && request.TotalAmount > senderWallet.BondBalanceRule)
        {
            logger.LogWarning("Amount exceeds the bond balance rule while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.BondBalanceRule}"
            };
        }
        
        // Check if the amount is within the maintaining balance rule
        if (senderWallet.MaintainingBalanceRule.HasValue && senderWallet.Balance - totalDecrement < senderWallet.MaintainingBalanceRule)
        {
            logger.LogWarning("Amount exceeds the maintaining balance rule while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {senderWallet.MaintainingBalanceRule}"
            };
        }
        
        // Check if the recipient wallet is within the min transferable amount
        if (request.TotalAmount < recipientWallet.MinTransferRule)
        {
            logger.LogWarning("Amount is below the minimum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {recipientWallet.MinTransferRule}"
            };
        }
        
        // Check if the recipient wallet is within the max transferable amount
        if (request.TotalAmount > recipientWallet.MaxTransferRule)
        {
            logger.LogWarning("Amount exceeds the maximum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}", request.CredentialId, request.RecipientCredentialId);
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
            senderWallet.DebitOnHoldBalance += request.TotalAmount;
            senderWallet.TransferableBalance -= request.TotalAmount;
            
            recipientWallet.CreditOnHoldBalance += request.TotalAmount;
        }
        else
        {
            senderWallet.Balance -= totalDecrement;
            senderWallet.TransferableBalance -= totalDecrement;
            
            recipientWallet.Balance += totalIncrement;
            recipientWallet.TransferableBalance += totalIncrement;
        }

        // Record the transactions
        var senderTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = senderWallet.Id,
            Amount = request.TotalAmount,
            TransactionFee = transferDeductionType is TransferDeductionType.DeductFromSender ? request.TotalFee : 0,
            PreviousBalance = previousSenderBalance,
            PreviousDebitOnHoldBalance = previousSenderDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = previousSenderCreditOnHoldBalance,
            RunningDebitOnHoldBalance = senderWallet.DebitOnHoldBalance,
            RunningCreditOnHoldBalance = senderWallet.CreditOnHoldBalance,
            RunningBalance = senderWallet.Balance,
            RunningTotalBalance = senderWallet.TotalBalance,
            RunningAvailableBalance = senderWallet.AvailableBalance,
            Remarks = request.Remarks,
            Description = $"Transfered to {MaskFullName(recipientUser.Result.Response.IdentityInfo.FullName)}",
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
            Amount = request.TotalAmount,
            TransactionFee = transferDeductionType is TransferDeductionType.DeductFromRecipient ? request.TotalFee : 0,
            PreviousBalance = previousRecipientBalance,
            PreviousDebitOnHoldBalance = previousRecipientDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = previousRecipientCreditOnHoldBalance,
            RunningBalance = recipientWallet.Balance,
            RunningTotalBalance = recipientWallet.TotalBalance,
            RunningAvailableBalance = recipientWallet.AvailableBalance,
            RunningCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance,
            RunningDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance,
            Remarks = request.Remarks,
            Description = $"Received from {MaskFullName(senderUser.Result.Response.IdentityInfo.FullName)}",
            TransactionType = TransactionType.Credit,
            Held = request.OnHold,
            Released = false,
            ReferenceNumber = request.ReferenceNumber
        };

        // Create WalletTransfer entity
        var walletTransfer = new WalletTransfer
        {
            SenderTransactionId = senderTransaction.Id,
            RecipientTransactionId = recipientTransaction.Id,
            SenderTransaction = senderTransaction,
            RecipientTransaction = recipientTransaction,
            LineItems = request.LineItems,
            TransactionPurpose = request.TransactionPurpose,
            TransactionFee = request.Fee
        };

        dbContext.Set<WalletTransfer>().Add(walletTransfer);

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

    private string MaskFullName(string fullname)
    {
        // Split the full name into words, removing any empty entries caused by extra spaces
        var names = fullname.Split(' ', StringSplitOptions.RemoveEmptyEntries).AsSpan();
        var maskedNameBuilder = new StringBuilder();

        for (int i = 0; i < names.Length; i++)
        {
            var word = names[i];
            if (word.Length > 1)
            {
                // Append first character
                maskedNameBuilder.Append(word[0]);
        
                // Append '*' for each middle character
                maskedNameBuilder.Append('*', word.Length - 2);
        
                // Append last character
                maskedNameBuilder.Append(word[^1]);
            }
            else
            {
                // If the word is a single character, just append it
                maskedNameBuilder.Append(word);
            }

            // Add a space after each word, but avoid it after the last word
            if (i < names.Length - 1)
            {
                maskedNameBuilder.Append(' ');
            }
        }

        // No need to trim as we manage spaces correctly
        return maskedNameBuilder.ToString();
    }

}